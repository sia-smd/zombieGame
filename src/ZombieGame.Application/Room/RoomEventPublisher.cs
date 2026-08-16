namespace ZombieGame.Application.Room;

using ZombieGame.Application.DTOs.Room;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public sealed class RoomEventPublisher : IRoomEventPublisher
{
    private readonly IRoomRealtimeNotifier _notifier;

    public RoomEventPublisher(IRoomRealtimeNotifier notifier) => _notifier = notifier;

    public async Task PublishAsync(RoomState room, IEnumerable<RoomEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case PhaseChangedEvent phase:
                    await _notifier.PhaseChangedAsync(room.MatchId, new { phase = phase.Phase, message = phase.Message }, cancellationToken);
                    break;
                case InvitationSentEvent sent:
                    await _notifier.InvitationSentAsync(room.MatchId, sent.Invitation, cancellationToken);
                    break;
                case InvitationAcceptedEvent accepted:
                    await _notifier.InvitationAcceptedAsync(room.MatchId, new { accepted.Invitation, accepted.Pair }, cancellationToken);
                    break;
                case InvitationsCancelledEvent cancelled:
                    await _notifier.InvitationsCancelledAsync(room.MatchId, cancelled.Cancelled, cancellationToken);
                    break;
                case BattleStartedEvent started:
                    await _notifier.BattleStartedAsync(room.MatchId, started.Pair.PairId, started.Pair, cancellationToken);
                    break;
                case BattleFinishedEvent finished:
                    await _notifier.BattleFinishedAsync(room.MatchId, finished.Summary, cancellationToken);
                    break;
                case PlayerEliminatedEvent eliminated:
                    await _notifier.PlayerEliminatedAsync(room.MatchId, eliminated.PlayerId, cancellationToken);
                    break;
                case DayStartedEvent day:
                    await _notifier.DayStartedAsync(room.MatchId, day.DayNumber, day.DayEvent, cancellationToken);
                    break;
                case GameFinishedEvent finished:
                    await _notifier.GameFinishedAsync(room.MatchId, new { winner = finished.Winner }, cancellationToken);
                    break;
                case ChatMessageEvent chat:
                    await _notifier.ChatMessageAsync(room.MatchId, chat.Message, cancellationToken);
                    break;
                case VoteUpdatedEvent vote:
                    await _notifier.VoteUpdatedAsync(room.MatchId, new { vote.VoteCount }, cancellationToken);
                    break;
            }
        }
    }
}

public sealed class NullRoomRealtimeNotifier : IRoomRealtimeNotifier
{
    public Task RoomUpdatedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PlayerJoinedAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task GameStartedAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PhaseChangedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InvitationSentAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InvitationAcceptedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InvitationsCancelledAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BattleStartedAsync(Guid matchId, Guid pairId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BattleFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BattleStateAsync(Guid pairId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PlayerRoomUpdatedAsync(Guid userId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DiscussionStartedAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ChatMessageAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task VoteStartedAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task VoteUpdatedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task VoteFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PlayerEliminatedAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DayStartedAsync(Guid matchId, int dayNumber, DayEventType dayEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task GameFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RoomInviteReceivedAsync(Guid userId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RoomInviteResolvedAsync(Guid userId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
