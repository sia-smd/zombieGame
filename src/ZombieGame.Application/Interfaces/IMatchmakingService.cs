namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.DTOs.Matchmaking;

public interface IMatchmakingService
{
    Task<JoinQueueResponse> JoinQueueAsync(Guid userId, CancellationToken cancellationToken = default);
    Task LeaveQueueAsync(Guid userId);
    bool IsInQueue(Guid userId);
    int QueueCount { get; }
}
