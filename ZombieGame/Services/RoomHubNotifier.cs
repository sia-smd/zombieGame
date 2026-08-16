namespace ZombieGame.Api.Services;

using Microsoft.AspNetCore.SignalR;
using ZombieGame.Api.Hubs;
using ZombieGame.Application.Interfaces;

public sealed class RoomHubNotifier : IRoomRealtimeNotifier
{
    private readonly IHubContext<RoomHub> _hub;

    public RoomHubNotifier(IHubContext<RoomHub> hub) => _hub = hub;

    public Task RoomUpdatedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("RoomUpdated", payload, cancellationToken);

    public Task PlayerJoinedAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("PlayerJoined", new { playerId }, cancellationToken);

    public Task GameStartedAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("GameStarted", cancellationToken);

    public Task PhaseChangedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("PhaseChanged", payload, cancellationToken);

    public Task InvitationSentAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("InvitationSent", payload, cancellationToken);

    public Task InvitationAcceptedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("InvitationAccepted", payload, cancellationToken);

    public Task InvitationsCancelledAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("InvitationsCancelled", payload, cancellationToken);

    public Task BattleStartedAsync(Guid matchId, Guid pairId, object payload, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            _hub.Clients.Group(RoomGroup(matchId)).SendAsync("BattleStarted", payload, cancellationToken),
            _hub.Clients.Group(BattleGroup(pairId)).SendAsync("BattleStarted", payload, cancellationToken));

    public Task BattleFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("BattleFinished", payload, cancellationToken);

    public Task BattleStateAsync(Guid pairId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(BattleGroup(pairId)).SendAsync("BattleState", payload, cancellationToken);

    public Task DiscussionStartedAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("DiscussionStarted", cancellationToken);

    public Task ChatMessageAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("ChatMessage", payload, cancellationToken);

    public Task VoteStartedAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("VoteStarted", cancellationToken);

    public Task VoteUpdatedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("VoteUpdated", payload, cancellationToken);

    public Task VoteFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("VoteFinished", payload, cancellationToken);

    public Task PlayerEliminatedAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("PlayerEliminated", new { playerId }, cancellationToken);

    public Task DayStartedAsync(Guid matchId, int dayNumber, ZombieGame.Domain.Enums.DayEventType dayEvent, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("DayStarted", new { dayNumber, dayEvent }, cancellationToken);

    public Task GameFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(RoomGroup(matchId)).SendAsync("GameFinished", payload, cancellationToken);

    public Task RoomInviteReceivedAsync(Guid userId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.User(userId.ToString()).SendAsync("RoomInviteReceived", payload, cancellationToken);

    public Task RoomInviteResolvedAsync(Guid userId, object payload, CancellationToken cancellationToken = default) =>
        _hub.Clients.User(userId.ToString()).SendAsync("RoomInviteResolved", payload, cancellationToken);

    private static string RoomGroup(Guid matchId) => $"room-{matchId}";
    private static string BattleGroup(Guid pairId) => $"battle-{pairId}";
}
