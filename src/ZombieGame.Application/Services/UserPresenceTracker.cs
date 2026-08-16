namespace ZombieGame.Application.Services;

using System.Collections.Concurrent;
using ZombieGame.Application.Interfaces;

public sealed class UserPresenceTracker : IUserPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _connections = new();

    public void AddConnection(Guid userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        set[connectionId] = 0;
    }

    public void RemoveConnection(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set))
            return;

        set.TryRemove(connectionId, out _);
        if (set.IsEmpty)
            _connections.TryRemove(userId, out _);
    }

    public bool IsOnline(Guid userId) =>
        _connections.TryGetValue(userId, out var set) && !set.IsEmpty;
}
