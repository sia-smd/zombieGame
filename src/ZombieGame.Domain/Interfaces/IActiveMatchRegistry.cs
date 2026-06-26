namespace ZombieGame.Domain.Interfaces;

public interface IActiveMatchRegistry
{
    Task RegisterAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task UnregisterAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetActiveMatchIdsAsync(CancellationToken cancellationToken = default);
}
