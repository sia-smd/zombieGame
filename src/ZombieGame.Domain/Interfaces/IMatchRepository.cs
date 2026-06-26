namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Match?> GetBySessionTokenAsync(string sessionToken, CancellationToken cancellationToken = default);
    Task<Match?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Match>> GetActiveMatchesAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Match match, CancellationToken cancellationToken = default);
    void Update(Match match);
}
