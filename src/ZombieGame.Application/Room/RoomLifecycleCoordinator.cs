namespace ZombieGame.Application.Room;

using ZombieGame.Application.Interfaces;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.Room.Bots;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

/// <summary>Cross-cutting lifecycle: snapshots, event log, player presence, match summary.</summary>
public interface IRoomLifecycleCoordinator
{
    Task OnPhaseEnteredAsync(RoomState room, RoomPhase phase, CancellationToken cancellationToken = default);
    Task OnPhaseCompletedAsync(RoomState room, RoomPhase completedPhase, CancellationToken cancellationToken = default);
    Task OnMatchFinishedAsync(RoomState room, CancellationToken cancellationToken = default);
    Task SyncPlayerPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default);
}

public sealed class RoomLifecycleCoordinator : IRoomLifecycleCoordinator
{
    private readonly IRoomSnapshotService _snapshots;
    private readonly IMatchEventLogService _eventLog;
    private readonly IPlayerReconnectService _reconnect;
    private readonly IMatchSummaryService _summary;
    private readonly IPlayerActiveMatchStore _activeStore;
    private readonly IMatchRepository _matchRepository;
    private readonly IMatchCompletionService _matchCompletion;

    public RoomLifecycleCoordinator(
        IRoomSnapshotService snapshots,
        IMatchEventLogService eventLog,
        IPlayerReconnectService reconnect,
        IMatchSummaryService summary,
        IPlayerActiveMatchStore activeStore,
        IMatchRepository matchRepository,
        IMatchCompletionService matchCompletion)
    {
        _snapshots = snapshots;
        _eventLog = eventLog;
        _reconnect = reconnect;
        _summary = summary;
        _activeStore = activeStore;
        _matchRepository = matchRepository;
        _matchCompletion = matchCompletion;
    }

    public async Task OnPhaseEnteredAsync(RoomState room, RoomPhase phase, CancellationToken cancellationToken = default)
    {
        PhaseReadyService.Clear(room);
        RoomBotScheduler.ClearBotSchedules(room);

        var eventType = phase switch
        {
            RoomPhase.Discussion => MatchEventType.DiscussionStarted,
            RoomPhase.Voting => MatchEventType.VotingStarted,
            RoomPhase.CardBattle => MatchEventType.BattleStarted,
            _ => (MatchEventType?)null
        };

        if (eventType is MatchEventType et)
            await _eventLog.LogAsync(room.MatchId, et, cancellationToken: cancellationToken);

        if (phase == RoomPhase.DayStart && room.DayNumber == 1)
        {
            room.MatchStartedAt ??= DateTime.UtcNow;
            room.Statistics.MatchStartedAt = room.MatchStartedAt;
            await _eventLog.LogAsync(room.MatchId, MatchEventType.MatchStarted, cancellationToken: cancellationToken);
        }

        if (phase == RoomPhase.DayStart)
            await _eventLog.LogAsync(room.MatchId, MatchEventType.DayStarted, payload: new { room.DayNumber }, cancellationToken: cancellationToken);
    }

    public async Task OnPhaseCompletedAsync(RoomState room, RoomPhase completedPhase, CancellationToken cancellationToken = default)
    {
        var kind = completedPhase switch
        {
            RoomPhase.CardBattle => SnapshotKind.EndOfBattle,
            RoomPhase.Discussion => SnapshotKind.EndOfDiscussion,
            RoomPhase.Voting => SnapshotKind.EndOfVoting,
            RoomPhase.VoteResult => SnapshotKind.EndOfDay,
            _ => (SnapshotKind?)null
        };

        if (kind is SnapshotKind snapshotKind)
            await _snapshots.CaptureAsync(room, snapshotKind, cancellationToken);

        if (completedPhase == RoomPhase.Voting)
            await _eventLog.LogAsync(room.MatchId, MatchEventType.VoteFinished, payload: new { room.Votes.Count }, cancellationToken: cancellationToken);
    }

    public async Task OnMatchFinishedAsync(RoomState room, CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(room.MatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Match {room.MatchId} was not found during finalization.");

        if (match.Status != MatchStatus.Finished)
        {
            room.Session.MatchId = room.MatchId;
            room.Session.WinTeam = room.WinTeam;
            if (room.DayNumber > 0)
                room.Session.TurnNumber = room.DayNumber;

            foreach (var player in room.Players)
            {
                var sessionPlayer = room.Session.GetPlayer(player.UserId);
                if (sessionPlayer is not null)
                    sessionPlayer.IsAlive = player.IsAlive;
            }

            await _matchCompletion.CompleteMatchAsync(
                match,
                room.Session,
                room.WinTeam,
                cancellationToken);
        }

        await _snapshots.CaptureAsync(room, SnapshotKind.EndOfMatch, cancellationToken);
        await _eventLog.LogAsync(room.MatchId, MatchEventType.GameFinished, payload: new { winner = room.WinTeam.ToString() }, cancellationToken: cancellationToken);
        await _summary.FinalizeAsync(room, cancellationToken);

        foreach (var player in room.Players)
            await _activeStore.RemoveAsync(player.UserId, cancellationToken);
    }

    public Task SyncPlayerPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default) =>
        _reconnect.TrackPresenceAsync(room, userId, sessionToken, cancellationToken);
}
