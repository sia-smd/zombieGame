namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        Guid userId,
        Guid matchId,
        TransactionType type,
        CancellationToken cancellationToken = default);
}
