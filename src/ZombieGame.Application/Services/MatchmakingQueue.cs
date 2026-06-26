namespace ZombieGame.Application.Services;

using System.Collections.Concurrent;
using ZombieGame.Application.Interfaces;

public class MatchmakingQueue : IMatchmakingQueue
{
    private readonly ConcurrentQueue<Guid> _queue = new();
    private readonly ConcurrentDictionary<Guid, byte> _queuedUsers = new();

    public int Count => _queuedUsers.Count;

    public bool Contains(Guid userId) => _queuedUsers.ContainsKey(userId);

    public bool TryEnqueue(Guid userId)
    {
        if (!_queuedUsers.TryAdd(userId, 0))
            return false;

        _queue.Enqueue(userId);
        return true;
    }

    public bool TryRemove(Guid userId) => _queuedUsers.TryRemove(userId, out _);

    public bool TryDequeueBatch(int count, out List<Guid> userIds)
    {
        userIds = new List<Guid>(count);
        while (userIds.Count < count && _queue.TryDequeue(out var userId))
        {
            if (_queuedUsers.TryRemove(userId, out _))
                userIds.Add(userId);
        }

        return userIds.Count == count;
    }

    public void Requeue(IEnumerable<Guid> userIds)
    {
        foreach (var userId in userIds)
        {
            if (_queuedUsers.TryAdd(userId, 0))
                _queue.Enqueue(userId);
        }
    }
}
