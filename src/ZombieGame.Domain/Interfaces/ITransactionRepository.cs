namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Entities;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default);
}
