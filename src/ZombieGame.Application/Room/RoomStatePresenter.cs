namespace ZombieGame.Application.Room;

using Microsoft.Extensions.Options;
using ZombieGame.Application.DTOs.Room;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public interface IRoomStatePresenter
{
    Task<RoomStateDto> BuildAsync(RoomState room, Guid viewerId, CancellationToken cancellationToken = default);

    /// <summary>Caller-scoped build that also carries the viewer's private battle data (hand/health).</summary>
    Task<RoomStateDto> BuildPrivateAsync(RoomState room, Guid viewerId, CancellationToken cancellationToken = default);

    Task BroadcastAsync(RoomState room, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a private room snapshot to each human in the pair so role/hand changes
    /// (e.g. infection) reach the victim immediately — group broadcasts omit <c>me</c>.
    /// </summary>
    Task PushPrivateToPairAsync(RoomState room, Guid pairId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the public room DTO and pushes it to the room group. Server-driven changes
/// (phase timers, invitation timeouts, bot actions) have no hub call to piggyback on,
/// so they broadcast through here instead.
/// </summary>
public sealed class RoomStatePresenter : IRoomStatePresenter
{
    private static readonly IReadOnlySet<Guid> NoOpponents = new HashSet<Guid>();

    private readonly IRoomRealtimeNotifier _notifier;
    private readonly OpponentSelectionService _opponentSelection;
    private readonly IBattlePairHistoryStore _history;
    private readonly GameSettings _gameSettings;
    private readonly RoomSettings _roomSettings;

    public RoomStatePresenter(
        IRoomRealtimeNotifier notifier,
        OpponentSelectionService opponentSelection,
        IBattlePairHistoryStore history,
        IOptions<GameSettings> gameSettings,
        IOptions<RoomSettings> roomSettings)
    {
        _notifier = notifier;
        _opponentSelection = opponentSelection;
        _history = history;
        _gameSettings = gameSettings.Value;
        _roomSettings = roomSettings.Value;
    }

    public Task<RoomStateDto> BuildAsync(
        RoomState room,
        Guid viewerId,
        CancellationToken cancellationToken = default) =>
        BuildForViewerAsync(room, viewerId, includePrivate: false, cancellationToken);

    public Task<RoomStateDto> BuildPrivateAsync(
        RoomState room,
        Guid viewerId,
        CancellationToken cancellationToken = default) =>
        BuildForViewerAsync(room, viewerId, includePrivate: true, cancellationToken);

    private async Task<RoomStateDto> BuildForViewerAsync(
        RoomState room,
        Guid viewerId,
        bool includePrivate,
        CancellationToken cancellationToken)
    {
        var revealVotes = room.CurrentPhase is RoomPhase.VoteResult or RoomPhase.Finished;
        return RoomStateMapper.ToPublicDto(
            room,
            viewerId,
            revealVotes: revealVotes,
            botFillTimeoutSeconds: _gameSettings.MatchmakingBotFillTimeoutSeconds,
            invitationTimeoutSeconds: _roomSettings.InvitationTimeoutSeconds,
            availableOpponentsByPlayer: await BuildAvailableOpponentsAsync(room, cancellationToken),
            includePrivate: includePrivate);
    }

    public async Task BroadcastAsync(RoomState room, CancellationToken cancellationToken = default)
    {
        var revealVotes = room.CurrentPhase is RoomPhase.VoteResult or RoomPhase.Finished;

        // No single viewer here, so the legacy per-viewer list stays empty and every client
        // reads its own entry from the per-player map.
        var state = RoomStateMapper.ToPublicDto(
            room,
            Guid.Empty,
            NoOpponents,
            revealVotes,
            _gameSettings.MatchmakingBotFillTimeoutSeconds,
            _roomSettings.InvitationTimeoutSeconds,
            await BuildAvailableOpponentsAsync(room, cancellationToken));

        await _notifier.RoomUpdatedAsync(room.MatchId, state, cancellationToken);
    }

    public async Task PushPrivateToPairAsync(
        RoomState room,
        Guid pairId,
        CancellationToken cancellationToken = default)
    {
        var pair = room.BattlePairs.FirstOrDefault(p => p.PairId == pairId);
        if (pair is null)
            return;

        foreach (var userId in new[] { pair.Player1Id, pair.Player2Id })
        {
            var player = room.GetPlayer(userId);
            if (player is null || player.IsBot)
                continue;

            var dto = await BuildPrivateAsync(room, userId, cancellationToken);
            await _notifier.PlayerRoomUpdatedAsync(userId, dto, cancellationToken);
        }
    }

    /// <summary>
    /// Room updates reach the whole group, so every player's invitable opponents travel in the
    /// same payload instead of only the acting player's.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> BuildAvailableOpponentsAsync(
        RoomState room,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, IReadOnlyList<Guid>>();
        if (room.CurrentPhase != RoomPhase.OpponentSelection)
            return map;

        foreach (var player in room.AlivePlayers.Where(p => !room.IsPaired(p.UserId)))
        {
            var opponents = await _opponentSelection.PeekAvailableOpponentsAsync(
                room, player.UserId, _history, cancellationToken);
            map[player.UserId] = opponents.ToList();
        }

        return map;
    }
}
