namespace ZombieGame.Application.Room;

using System.Text.Json;
using ZombieGame.Application.Common;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public interface IRoomCommandValidator
{
    Task ValidatePlayerCommandAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        RoomPhase requiredPhase,
        bool requireAlive = true,
        CancellationToken cancellationToken = default);

    Task ValidatePlayerCommandAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        IReadOnlyCollection<RoomPhase> allowedPhases,
        bool requireAlive = true,
        CancellationToken cancellationToken = default);

    Task ValidateBattleCommandAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid battleId,
        CancellationToken cancellationToken = default);
}

public sealed class RoomCommandValidator : IRoomCommandValidator
{
    private readonly IMatchRepository _matchRepository;
    private readonly IRoomStateStore _roomStore;
    private readonly IBattleStore _battleStore;

    public RoomCommandValidator(
        IMatchRepository matchRepository,
        IRoomStateStore roomStore,
        IBattleStore battleStore)
    {
        _matchRepository = matchRepository;
        _roomStore = roomStore;
        _battleStore = battleStore;
    }

    public Task ValidatePlayerCommandAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        RoomPhase requiredPhase,
        bool requireAlive = true,
        CancellationToken cancellationToken = default) =>
        ValidatePlayerCommandAsync(userId, matchId, sessionToken, new[] { requiredPhase }, requireAlive, cancellationToken);

    public async Task ValidatePlayerCommandAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        IReadOnlyCollection<RoomPhase> allowedPhases,
        bool requireAlive = true,
        CancellationToken cancellationToken = default)
    {
        await ValidateMatchMembershipAsync(userId, matchId, sessionToken, cancellationToken);

        var room = await _roomStore.GetAsync(matchId, cancellationToken)
            ?? throw new ServiceException("Room not found.");

        if (!allowedPhases.Contains(room.CurrentPhase))
            throw new ServiceException($"Command not allowed in phase {room.CurrentPhase}.");

        var player = room.GetPlayer(userId)
            ?? throw new ServiceException("Player not found.");

        if (requireAlive && !player.IsAlive)
            throw new ServiceException("Dead players cannot perform this action.");
    }

    public async Task ValidateBattleCommandAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid battleId,
        CancellationToken cancellationToken = default)
    {
        await ValidatePlayerCommandAsync(userId, matchId, sessionToken, RoomPhase.CardBattle, cancellationToken: cancellationToken);

        var room = await _roomStore.GetAsync(matchId, cancellationToken)
            ?? throw new ServiceException("Room not found.");

        // Reject day-1 PairIds that still sit in Redis after the day rolled over.
        if (!room.BattlePairs.Any(p => p.PairId == battleId))
            throw new ServiceException("Battle is not active.");

        var battle = await _battleStore.GetAsync(matchId, battleId, cancellationToken)
            ?? throw new ServiceException("Battle not found.");

        if (!battle.IsMember(userId))
            throw new ServiceException("You are not a member of this battle.");

        if (battle.Status != BattleAggregateStatus.InProgress)
            throw new ServiceException("Battle is not active.");
    }

    private async Task ValidateMatchMembershipAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken)
            ?? throw new ServiceException("Match not found.");

        if (!string.Equals(match.SessionToken, sessionToken, StringComparison.Ordinal))
            throw new ServiceException("Invalid session token.");

        if (!match.Players.Any(p => p.UserId == userId))
            throw new ServiceException("User is not in this match.");
    }
}

public interface IRoomSnapshotService
{
    Task CaptureAsync(RoomState room, SnapshotKind kind, CancellationToken cancellationToken = default);
}

public sealed class RoomSnapshotService : IRoomSnapshotService
{
    private readonly IRoomSnapshotStore _store;

    public RoomSnapshotService(IRoomSnapshotStore store) => _store = store;

    public Task CaptureAsync(RoomState room, SnapshotKind kind, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            room.DayNumber,
            phase = room.CurrentPhase.ToString(),
            alive = room.AlivePlayers.Select(p => p.UserId).ToList(),
            battleSummaries = room.CurrentDayBattleSummaries,
            votesCount = room.Votes.Count,
            winTeam = room.WinTeam.ToString()
        });

        return _store.AppendAsync(new RoomSnapshot
        {
            Id = Guid.NewGuid(),
            MatchId = room.MatchId,
            Kind = kind,
            DayNumber = room.DayNumber,
            Phase = room.CurrentPhase,
            CreatedAt = DateTime.UtcNow,
            PayloadJson = payload
        }, cancellationToken);
    }
}

public interface IMatchEventLogService
{
    Task LogAsync(Guid matchId, MatchEventType type, Guid? actorUserId = null, object? payload = null, CancellationToken cancellationToken = default);
}

public sealed class MatchEventLogService : IMatchEventLogService
{
    private readonly IMatchEventLogStore _store;

    public MatchEventLogService(IMatchEventLogStore store) => _store = store;

    public Task LogAsync(
        Guid matchId,
        MatchEventType type,
        Guid? actorUserId = null,
        object? payload = null,
        CancellationToken cancellationToken = default) =>
        _store.AppendAsync(new MatchEventEntry
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            EventType = type,
            OccurredAt = DateTime.UtcNow,
            ActorUserId = actorUserId,
            PayloadJson = payload is null ? "{}" : JsonSerializer.Serialize(payload)
        }, cancellationToken);
}

public interface IMatchSummaryService
{
    Task<MatchSummary> FinalizeAsync(RoomState room, CancellationToken cancellationToken = default);
}

public sealed class MatchSummaryService : IMatchSummaryService
{
    private readonly IMatchSummaryStore _store;

    public MatchSummaryService(IMatchSummaryStore store) => _store = store;

    public async Task<MatchSummary> FinalizeAsync(RoomState room, CancellationToken cancellationToken = default)
    {
        var stats = room.Statistics;
        var mostActive = stats.ActionsByPlayer.OrderByDescending(kv => kv.Value).FirstOrDefault();

        var summary = new MatchSummary
        {
            MatchId = room.MatchId,
            WinningTeam = room.WinTeam,
            TotalDays = room.DayNumber,
            Battles = stats.TotalBattles,
            Infections = stats.TotalInfections,
            Kills = stats.TotalKills,
            Eliminations = stats.TotalEliminations,
            Votes = stats.TotalVotes,
            CardsPlayed = stats.TotalCardsPlayed,
            MostActivePlayerId = mostActive.Key == Guid.Empty ? null : mostActive.Key,
            MvpPlayerId = mostActive.Key == Guid.Empty ? null : mostActive.Key,
            Duration = stats.MatchStartedAt is null
                ? TimeSpan.Zero
                : DateTime.UtcNow - stats.MatchStartedAt.Value,
            FinishedAt = DateTime.UtcNow
        };

        await _store.SaveAsync(summary, cancellationToken);
        return summary;
    }
}

public interface IPlayerReconnectService
{
    Task<PlayerActiveMatch> TrackPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default);
    /// <summary>Returns the match the player was in, or null when there is nothing to track.</summary>
    Task<PlayerActiveMatch?> OnDisconnectedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ActiveMatchResponse?> GetActiveMatchAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ResumeMatchResponse?> ResumeAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
}

public sealed class PlayerReconnectService : IPlayerReconnectService
{
    private readonly IPlayerActiveMatchStore _activeStore;
    private readonly IRoomStateStore _roomStore;
    private readonly IBattleStore _battleStore;
    private readonly IMatchRepository _matchRepository;
    private readonly IMatchEventLogService _eventLog;

    public PlayerReconnectService(
        IPlayerActiveMatchStore activeStore,
        IRoomStateStore roomStore,
        IBattleStore battleStore,
        IMatchRepository matchRepository,
        IMatchEventLogService eventLog)
    {
        _activeStore = activeStore;
        _roomStore = roomStore;
        _battleStore = battleStore;
        _matchRepository = matchRepository;
        _eventLog = eventLog;
    }

    public async Task<PlayerActiveMatch> TrackPresenceAsync(
        RoomState room,
        Guid userId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var sessionPlayer = room.Session.GetPlayer(userId);
        Guid? battleId = room.CurrentPhase == RoomPhase.CardBattle
            ? room.BattlePairs.FirstOrDefault(p => p.Player1Id == userId || p.Player2Id == userId)?.PairId
            : null;

        var entry = new PlayerActiveMatch
        {
            PlayerId = userId,
            MatchId = room.MatchId,
            SessionToken = sessionToken,
            CurrentBattleId = battleId,
            CurrentRoomPhase = room.CurrentPhase,
            DayNumber = room.DayNumber,
            IsAlive = room.GetPlayer(userId)?.IsAlive ?? false,
            Role = sessionPlayer?.Role ?? PlayerRole.Unknown,
            ConnectionRole = MatchConnectionRole.Player,
            LastSeen = DateTime.UtcNow,
            RoomName = (await _matchRepository.GetByIdAsync(room.MatchId, cancellationToken))?.Name
                ?? $"Match {room.MatchId.ToString()[..8]}"
        };

        await _activeStore.SetAsync(entry, cancellationToken);
        await _eventLog.LogAsync(room.MatchId, MatchEventType.PlayerReconnected, userId, cancellationToken: cancellationToken);
        return entry;
    }

    public async Task<PlayerActiveMatch?> OnDisconnectedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entry = await _activeStore.GetAsync(userId, cancellationToken);
        if (entry is null)
            return null;

        entry.LastSeen = DateTime.UtcNow;
        await _activeStore.SetAsync(entry, cancellationToken);
        await _eventLog.LogAsync(entry.MatchId, MatchEventType.PlayerDisconnected, userId, cancellationToken: cancellationToken);
        return entry;
    }

    public async Task<ActiveMatchResponse?> GetActiveMatchAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entry = await _activeStore.GetAsync(userId, cancellationToken);
        if (entry is null)
            return new ActiveMatchResponse(false);

        var match = await _matchRepository.GetByIdAsync(entry.MatchId, cancellationToken);
        if (match is null || match.Status == MatchStatus.Finished)
        {
            await _activeStore.RemoveAsync(userId, cancellationToken);
            return new ActiveMatchResponse(false);
        }

        var room = await _roomStore.GetAsync(entry.MatchId, cancellationToken);
        var aliveCount = room?.AlivePlayers.Count() ?? 0;

        return new ActiveMatchResponse(
            true,
            entry.MatchId,
            entry.SessionToken,
            entry.DayNumber,
            entry.CurrentRoomPhase,
            entry.CurrentBattleId,
            entry.IsAlive,
            entry.Role,
            entry.RoomName,
            aliveCount);
    }

    public async Task<ResumeMatchResponse?> ResumeAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var room = await _roomStore.GetAsync(matchId, cancellationToken);
        if (room is null)
            return null;

        var entry = await TrackPresenceAsync(room, userId, sessionToken, cancellationToken);
        Domain.Models.Room.Battle? battle = entry.CurrentBattleId is Guid bid
            ? await _battleStore.GetAsync(matchId, bid, cancellationToken)
            : null;

        return new ResumeMatchResponse(entry, battle);
    }
}

public sealed record ActiveMatchResponse(
    bool HasActiveMatch,
    Guid? MatchId = null,
    string? SessionToken = null,
    int Day = 0,
    RoomPhase Phase = RoomPhase.Lobby,
    Guid? BattleId = null,
    bool Alive = false,
    PlayerRole Role = PlayerRole.Unknown,
    string? RoomName = null,
    int PlayersAlive = 0);

public sealed record ResumeMatchResponse(PlayerActiveMatch Presence, Domain.Models.Room.Battle? CurrentBattle);
