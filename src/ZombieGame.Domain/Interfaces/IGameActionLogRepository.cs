namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Entities;

public interface IGameActionLogRepository
{
    Task<bool> ExistsByIdempotencyKeyAsync(Guid matchId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task AddAsync(GameActionLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameActionLog>> GetByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<GameActionLog?> GetLatestWithSnapshotAsync(Guid matchId, CancellationToken cancellationToken = default);
}
