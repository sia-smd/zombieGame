namespace ZombieGame.Application.Room;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public interface IRoomStateMachine
{
    Task<RoomState?> GetStateAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<RoomTransitionResult> DispatchAsync(Guid matchId, IRoomCommand command, CancellationToken cancellationToken = default);
    Task<RoomTransitionResult> TickAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<RoomState> InitializeRoomAsync(Match match, CancellationToken cancellationToken = default);
    Task SyncPlayersFromMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task RemovePlayerFromLobbyAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default);
    Task DiscardLobbyRoomAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<RoomTransitionResult> StartGameAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<RoomState?> SetPresenceAsync(Guid matchId, Guid userId, bool connected, CancellationToken cancellationToken = default);
}

public sealed class RoomStateMachine : IRoomStateMachine
{
    private readonly IRoomStateStore _store;
    private readonly IBattlePairHistoryStore _history;
    private readonly IDiscussionChatStore _chat;
    private readonly IRoomLock _lock;
    private readonly IActiveMatchRegistry _activeMatches;
    private readonly IUserRepository _userRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly RoomSettings _settings;
    private readonly IReadOnlyDictionary<RoomPhase, IRoomPhaseHandler> _handlers;
    private readonly IRoomEventPublisher _events;
    private readonly IRoomStatePresenter _presenter;
    private readonly IRoomLifecycleCoordinator _lifecycle;
    private readonly IBattleStore _battles;
    private readonly IRoomSnapshotStore _snapshots;
    private readonly IMatchEventLogStore _eventLog;
    private readonly ILogger<RoomStateMachine> _logger;

    public RoomStateMachine(
        IRoomStateStore store,
        IBattlePairHistoryStore history,
        IDiscussionChatStore chat,
        IRoomLock roomLock,
        IActiveMatchRegistry activeMatches,
        IUserRepository userRepository,
        IMatchRepository matchRepository,
        IEnumerable<IRoomPhaseHandler> handlers,
        IRoomEventPublisher events,
        IRoomStatePresenter presenter,
        IRoomLifecycleCoordinator lifecycle,
        IBattleStore battles,
        IRoomSnapshotStore snapshots,
        IMatchEventLogStore eventLog,
        IOptions<RoomSettings> settings,
        ILogger<RoomStateMachine> logger)
    {
        _store = store;
        _history = history;
        _chat = chat;
        _lock = roomLock;
        _activeMatches = activeMatches;
        _userRepository = userRepository;
        _matchRepository = matchRepository;
        _events = events;
        _presenter = presenter;
        _lifecycle = lifecycle;
        _battles = battles;
        _snapshots = snapshots;
        _eventLog = eventLog;
        _settings = settings.Value;
        _logger = logger;
        _handlers = handlers.ToDictionary(h => h.Phase);
    }

    public Task<RoomState?> GetStateAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _store.GetAsync(matchId, cancellationToken);

    public async Task<RoomState> InitializeRoomAsync(Match match, CancellationToken cancellationToken = default)
    {
        var players = new List<RoomPlayerState>();
        var sessionPlayers = new List<GamePlayerState>();

        foreach (var mp in match.Players.OrderBy(p => p.SeatIndex))
        {
            var user = await _userRepository.GetByIdWithProfileAsync(mp.UserId, cancellationToken);
            players.Add(new RoomPlayerState
            {
                UserId = mp.UserId,
                Username = user?.Username ?? "Unknown",
                ImageId = ResolveImageId(user),
                IsBot = mp.IsBot,
                IsAlive = mp.IsAlive,
                SeatIndex = mp.SeatIndex
            });

            sessionPlayers.Add(new GamePlayerState
            {
                UserId = mp.UserId,
                Username = user?.Username ?? "Unknown",
                IsBot = mp.IsBot,
                IsAlive = mp.IsAlive,
                SeatIndex = mp.SeatIndex,
                Role = mp.Role
            });
        }

        var hostId = match.Players.OrderBy(p => p.JoinedAt).ThenBy(p => p.SeatIndex).FirstOrDefault()?.UserId;

        var room = new RoomState
        {
            MatchId = match.Id,
            SessionToken = match.SessionToken,
            RoomName = match.Name,
            MaxPlayers = match.MaxPlayers,
            FillWithBots = match.FillWithBots,
            CreatedAt = match.CreatedAt,
            HostUserId = hostId,
            CurrentPhase = RoomPhase.Lobby,
            DayNumber = 0,
            Session = new GameSessionState
            {
                MatchId = match.Id,
                SessionToken = match.SessionToken,
                CurrentPhase = GamePhase.Lobby,
                Players = sessionPlayers,
                PlayerHands = sessionPlayers.Select(p => new PlayerCardState { UserId = p.UserId }).ToList()
            },
            Players = players
        };

        if (await _store.TryAddAsync(match.Id, room, cancellationToken))
            return room;

        // Never last-write-wins over a room that already exists (may already be in-game).
        var existing = await _store.GetAsync(match.Id, cancellationToken);
        return existing ?? room;
    }

    public async Task SyncPlayersFromMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);
        if (match is null)
            return;

        await using var guard = await AcquireLockAsync(matchId, cancellationToken);
        if (guard is null)
            return;

        var room = await _store.GetAsync(matchId, cancellationToken);
        if (room is null)
        {
            await InitializeRoomAsync(match, cancellationToken);
            return;
        }

        var matchIds = match.Players.Select(p => p.UserId).ToHashSet();
        var existingIds = room.Players.Select(p => p.UserId).ToHashSet();
        var changed = false;

        foreach (var leftover in room.Players.Where(p => !matchIds.Contains(p.UserId)).ToList())
        {
            StripLobbyPlayer(room, leftover.UserId);
            changed = true;
        }

        foreach (var mp in match.Players.OrderBy(p => p.SeatIndex))
        {
            if (existingIds.Contains(mp.UserId))
                continue;

            var user = await _userRepository.GetByIdWithProfileAsync(mp.UserId, cancellationToken);
            var username = user?.Username ?? "Unknown";
            room.Players.Add(new RoomPlayerState
            {
                UserId = mp.UserId,
                Username = username,
                ImageId = ResolveImageId(user),
                IsBot = mp.IsBot,
                IsAlive = mp.IsAlive,
                SeatIndex = mp.SeatIndex
            });
            room.Session.Players.Add(new GamePlayerState
            {
                UserId = mp.UserId,
                Username = username,
                IsBot = mp.IsBot,
                IsAlive = mp.IsAlive,
                SeatIndex = mp.SeatIndex,
                Role = mp.Role
            });
            room.Session.PlayerHands.Add(new PlayerCardState { UserId = mp.UserId });
            changed = true;
        }

        if (changed)
        {
            await _store.SetAsync(room, cancellationToken);
            await BroadcastStateAsync(room, cancellationToken);
        }
    }

    public async Task RemovePlayerFromLobbyAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);
        var room = await _store.GetAsync(matchId, cancellationToken);
        if (room is null)
            return;

        if (room.CurrentPhase != RoomPhase.Lobby)
            throw new ServiceException("Can only leave a waiting room.");

        StripLobbyPlayer(room, userId);
        await _store.SetAsync(room, cancellationToken);
        await BroadcastStateAsync(room, cancellationToken);
    }

    public async Task DiscardLobbyRoomAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);
        await CleanupRoomAsync(matchId, cancellationToken);
        await _activeMatches.UnregisterAsync(matchId, cancellationToken);
        await _store.RemoveAsync(matchId, cancellationToken);
        _logger.LogInformation("Empty waiting room {MatchId} removed from the room store.", matchId);
    }

    private static void StripLobbyPlayer(RoomState room, Guid userId)
    {
        room.Players.RemoveAll(p => p.UserId == userId);
        room.Session.Players.RemoveAll(p => p.UserId == userId);
        room.Session.PlayerHands.RemoveAll(h => h.UserId == userId);
        room.Votes.Remove(userId);
        room.PhaseReadyPlayers.Remove(userId);
        if (room.HostUserId == userId)
        {
            room.HostUserId = room.Players
                .Where(p => !p.IsBot)
                .OrderBy(p => p.SeatIndex)
                .FirstOrDefault()?.UserId
                ?? room.Players.OrderBy(p => p.SeatIndex).FirstOrDefault()?.UserId;
        }
    }

    public async Task<RoomTransitionResult> StartGameAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);
        var room = await RequireRoomAsync(matchId, cancellationToken);
        if (room.CurrentPhase != RoomPhase.Lobby)
            return RoomTransitionResult.Stay("Game already started.");

        room.DayNumber = 1;
        await _activeMatches.RegisterAsync(matchId, cancellationToken);
        return await TransitionToAsync(room, RoomPhase.DayStart, cancellationToken);
    }

    /// <summary>
    /// Records connect/disconnect so the room list can show a Disconnected badge and battles can
    /// auto-pass players who never come back (Room Flow V2).
    /// </summary>
    public async Task<RoomState?> SetPresenceAsync(
        Guid matchId,
        Guid userId,
        bool connected,
        CancellationToken cancellationToken = default)
    {
        await using var guard = await AcquireLockAsync(matchId, cancellationToken);
        if (guard is null)
            return await _store.GetAsync(matchId, cancellationToken);

        var room = await _store.GetAsync(matchId, cancellationToken);
        if (room is null)
            return null;

        var player = room.GetPlayer(userId);
        if (player is null)
            return room;

        var wasDisconnected = player.IsDisconnected;
        if (connected)
            RoomActivityTracker.MarkConnected(room, userId);
        else
            RoomActivityTracker.MarkDisconnected(room, userId);

        if (wasDisconnected != player.IsDisconnected)
            await _store.SetAsync(room, cancellationToken);

        return room;
    }

    public async Task<RoomTransitionResult> DispatchAsync(Guid matchId, IRoomCommand command, CancellationToken cancellationToken = default)
    {
        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);

        var room = await RequireRoomAsync(matchId, cancellationToken);
        var context = BuildContext(room);

        if (!_handlers.TryGetValue(room.CurrentPhase, out var handler))
            throw new ServiceException($"No handler for phase {room.CurrentPhase}.");

        var result = await handler.HandleAsync(context, command, cancellationToken);
        await ApplyTransitionAsync(room, result, cancellationToken);
        return result;
    }

    public async Task<RoomTransitionResult> TickAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        await using var guard = await AcquireLockAsync(matchId, cancellationToken);
        if (guard is null)
            return RoomTransitionResult.Stay();

        var room = await _store.GetAsync(matchId, cancellationToken);
        if (room is null)
            return RoomTransitionResult.Stay();

        // A previous finalization attempt may have persisted the terminal room
        // state but failed before unregistering/cleanup. Keep retrying while the
        // room remains in the active registry.
        if (room.CurrentPhase == RoomPhase.Finished)
        {
            await FinalizeRoomAsync(room, cancellationToken);
            return RoomTransitionResult.Stay("Game finalization completed.");
        }

        if (room.IsFinished)
            return RoomTransitionResult.Stay();

        if (!_handlers.TryGetValue(room.CurrentPhase, out var handler))
            return RoomTransitionResult.Stay();

        var result = await handler.OnTickAsync(BuildContext(room), cancellationToken);
        await ApplyTransitionAsync(room, result, cancellationToken);
        return result;
    }

    private async Task ApplyTransitionAsync(RoomState room, RoomTransitionResult result, CancellationToken cancellationToken)
    {
        var previousPhase = room.CurrentPhase;
        await _store.SetAsync(room, cancellationToken);
        await _events.PublishAsync(room, result.Events, cancellationToken);

        if (result.Advanced && result.NextPhase is RoomPhase next)
        {
            await _lifecycle.OnPhaseCompletedAsync(room, previousPhase, cancellationToken);
            await TransitionToAsync(room, next, cancellationToken);
            return;
        }

        // Idle ticks change nothing, so only event-producing changes are worth a payload.
        if (result.Events.Count > 0)
            await BroadcastStateAsync(room, cancellationToken);
    }

    private async Task<RoomTransitionResult> TransitionToAsync(
        RoomState room,
        RoomPhase nextPhase,
        CancellationToken cancellationToken)
    {
        room.CurrentPhase = nextPhase;

        if (!_handlers.TryGetValue(nextPhase, out var handler))
            throw new ServiceException($"No handler for phase {nextPhase}.");

        await _lifecycle.OnPhaseEnteredAsync(room, nextPhase, cancellationToken);

        var enterResult = await handler.OnEnterAsync(BuildContext(room), cancellationToken);
        await _store.SetAsync(room, cancellationToken);

        var events = enterResult.Events.Prepend(new PhaseChangedEvent(nextPhase, enterResult.Message)).ToList();
        await _events.PublishAsync(room, events, cancellationToken);

        if (enterResult.Advanced && enterResult.NextPhase is RoomPhase chained)
            return await TransitionToAsync(room, chained, cancellationToken);

        // Persist MatchPlayer roles before the final broadcast/teardown so the end-report
        // API has roles even if a client misses the Finished RoomUpdated payload.
        if (nextPhase == RoomPhase.Finished)
            await _lifecycle.OnMatchFinishedAsync(room, cancellationToken);

        await BroadcastStateAsync(room, cancellationToken);

        if (nextPhase == RoomPhase.Finished)
            await CleanupAfterFinishedAsync(room, cancellationToken);

        return enterResult;
    }

    private async Task CleanupAfterFinishedAsync(RoomState room, CancellationToken cancellationToken)
    {
        await CleanupRoomAsync(room.MatchId, cancellationToken);
        await _activeMatches.UnregisterAsync(room.MatchId, cancellationToken);
        await _store.RemoveAsync(room.MatchId, cancellationToken);
        _logger.LogInformation("Room {MatchId} cleaned up after finish.", room.MatchId);
    }

    private async Task FinalizeRoomAsync(RoomState room, CancellationToken cancellationToken)
    {
        await _lifecycle.OnMatchFinishedAsync(room, cancellationToken);
        await CleanupAfterFinishedAsync(room, cancellationToken);
    }

    /// <summary>A broken client connection must never stop the room loop.</summary>
    private async Task BroadcastStateAsync(RoomState room, CancellationToken cancellationToken)
    {
        try
        {
            await _presenter.BroadcastAsync(room, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast room state for {MatchId}.", room.MatchId);
        }
    }

    private async Task CleanupRoomAsync(Guid matchId, CancellationToken cancellationToken)
    {
        await _history.RemoveRoomAsync(matchId, cancellationToken);
        await _chat.RemoveRoomAsync(matchId, cancellationToken);
        await _battles.RemoveMatchAsync(matchId, cancellationToken);
        await _snapshots.RemoveMatchAsync(matchId, cancellationToken);
        await _eventLog.RemoveMatchAsync(matchId, cancellationToken);
    }

    private RoomContext BuildContext(RoomState room) =>
        new()
        {
            Room = room,
            Settings = _settings,
            History = _history,
            Chat = _chat
        };

    private async Task<RoomState> RequireRoomAsync(Guid matchId, CancellationToken cancellationToken) =>
        await _store.GetAsync(matchId, cancellationToken)
        ?? throw new ServiceException("Room not found.");

    private async Task<IAsyncDisposable> AcquireRequiredLockAsync(Guid matchId, CancellationToken cancellationToken) =>
        await AcquireLockAsync(matchId, cancellationToken)
            ?? throw new ServiceException("Room is busy. Try again.");

    /// <summary>
    /// Tries to acquire the distributed room lock, retrying until <see cref="RoomSettings.LockWaitMilliseconds"/>.
    /// Lease TTL is <see cref="RoomSettings.LockTtlSeconds"/> — if a command outlives the lease another node can enter.
    /// </summary>
    private async Task<IAsyncDisposable?> AcquireLockAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var lease = TimeSpan.FromSeconds(Math.Max(1, _settings.LockTtlSeconds));
        var wait = TimeSpan.FromMilliseconds(Math.Max(0, _settings.LockWaitMilliseconds));
        var deadline = DateTime.UtcNow + wait;

        while (true)
        {
            var guard = await _lock.TryAcquireAsync(matchId, lease, cancellationToken);
            if (guard is not null)
                return guard;

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return null;

            var delay = remaining < TimeSpan.FromMilliseconds(20) ? remaining : TimeSpan.FromMilliseconds(20);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static string ResolveImageId(User? user) =>
        string.IsNullOrWhiteSpace(user?.Profile?.ImageId) ? "avatar_default_01" : user.Profile.ImageId;
}
