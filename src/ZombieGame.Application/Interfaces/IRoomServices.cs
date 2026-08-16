namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.Room;
using ZombieGame.Application.DTOs.Room;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public interface IRoomService
{
    Task<RoomActionResult> JoinRoomAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
    Task<RoomActionResult> StartGameAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
    Task<RoomActionResult> SendInvitationAsync(Guid userId, Guid matchId, string sessionToken, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<RoomActionResult> RespondInvitationAsync(Guid userId, Guid matchId, string sessionToken, Guid invitationId, bool accept, CancellationToken cancellationToken = default);
    Task<RoomActionResult> PlayCardInBattleAsync(Guid userId, Guid matchId, string sessionToken, Guid pairId, Guid cardId, Guid? targetUserId, CancellationToken cancellationToken = default);
    Task<RoomActionResult> PassInBattleAsync(Guid userId, Guid matchId, string sessionToken, Guid pairId, CancellationToken cancellationToken = default);
    Task<RoomActionResult> SendChatAsync(Guid userId, Guid matchId, string sessionToken, string text, CancellationToken cancellationToken = default);
    Task<RoomActionResult> VoteAsync(Guid userId, Guid matchId, string sessionToken, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<RoomActionResult> MarkPhaseReadyAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
    Task<JoinBattleResult> JoinBattleAsync(Guid userId, Guid matchId, string sessionToken, Guid battleId, CancellationToken cancellationToken = default);
    Task<ResumeMatchResponse?> ResumeMatchAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
    /// <summary>Marks the player offline; returns the refreshed room state to broadcast, if any.</summary>
    Task<RoomActionResult?> OnPlayerDisconnectedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RoomStateDto?> GetPublicStateAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageDto>> GetDiscussionAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public sealed record JoinBattleResult(bool Allowed, Guid BattleId, string? Message = null);

public interface IActiveMatchQueryService
{
    Task<ActiveMatchResponse> GetActiveMatchForPlayerAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IRoomEventPublisher
{
    Task PublishAsync(RoomState room, IEnumerable<RoomEvent> events, CancellationToken cancellationToken = default);
}

public interface IRoomRealtimeNotifier
{
    Task RoomUpdatedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task PlayerJoinedAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default);
    Task GameStartedAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task PhaseChangedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task InvitationSentAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task InvitationAcceptedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task InvitationsCancelledAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task BattleStartedAsync(Guid matchId, Guid pairId, object payload, CancellationToken cancellationToken = default);
    Task BattleFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task BattleStateAsync(Guid pairId, object payload, CancellationToken cancellationToken = default);
    /// <summary>Caller-scoped room snapshot (includes private <c>me</c>) for one connected user.</summary>
    Task PlayerRoomUpdatedAsync(Guid userId, object payload, CancellationToken cancellationToken = default);
    Task DiscussionStartedAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task ChatMessageAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task VoteStartedAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task VoteUpdatedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task VoteFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task PlayerEliminatedAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default);
    Task DayStartedAsync(Guid matchId, int dayNumber, DayEventType dayEvent, CancellationToken cancellationToken = default);
    Task GameFinishedAsync(Guid matchId, object payload, CancellationToken cancellationToken = default);
    Task RoomInviteReceivedAsync(Guid userId, object payload, CancellationToken cancellationToken = default);
    Task RoomInviteResolvedAsync(Guid userId, object payload, CancellationToken cancellationToken = default);
}

public interface IRoomLoopProcessor
{
    Task ProcessActiveRoomsAsync(CancellationToken cancellationToken = default);
}
