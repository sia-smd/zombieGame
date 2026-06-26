namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Models;

public interface IGameSessionStore
{
    Task<GameSessionState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<GameSessionState?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default);
    Task SetAsync(GameSessionState state, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<bool> TryAddAsync(Guid matchId, GameSessionState state, CancellationToken cancellationToken = default);
}
