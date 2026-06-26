namespace ZombieGame.Infrastructure.Game;

using System.Collections.Concurrent;
using ZombieGame.Domain.Interfaces;

public sealed class InMemoryActiveMatchRegistry : IActiveMatchRegistry
{
    private readonly ConcurrentDictionary<Guid, byte> _active = new();

    public Task RegisterAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        _active.TryAdd(matchId, 0);
        return Task.CompletedTask;
    }

    public Task UnregisterAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        _active.TryRemove(matchId, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetActiveMatchIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(_active.Keys.ToList());
}
