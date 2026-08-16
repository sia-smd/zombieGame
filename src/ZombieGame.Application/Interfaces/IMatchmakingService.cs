namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.DTOs.Matchmaking;

public interface IMatchmakingService
{
    Task<JoinQueueResponse> JoinQueueAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CreateRoomResponse> CreateRoomAsync(Guid userId, CreateRoomRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenRoomDto>> ListOpenRoomsAsync(CancellationToken cancellationToken = default);
    Task<JoinOpenRoomResponse> JoinOpenRoomAsync(Guid userId, JoinOpenRoomRequest request, CancellationToken cancellationToken = default);
    Task LeaveWaitingRoomAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
    Task<SendRoomInviteResponse> SendWaitingRoomInviteAsync(
        Guid fromUserId,
        SendRoomInviteRequest request,
        CancellationToken cancellationToken = default);
    Task<JoinOpenRoomResponse> AcceptWaitingRoomInviteAsync(
        Guid userId,
        Guid inviteId,
        CancellationToken cancellationToken = default);
    Task DenyWaitingRoomInviteAsync(Guid userId, Guid inviteId, CancellationToken cancellationToken = default);
    RoomConfigResponse GetRoomConfig();
    Task LeaveQueueAsync(Guid userId);
    Task ProcessQueueTimeoutsAsync(CancellationToken cancellationToken = default);
    Task ProcessWaitingRoomBotFillsAsync(CancellationToken cancellationToken = default);
    bool IsInQueue(Guid userId);
    int QueueCount { get; }
}
