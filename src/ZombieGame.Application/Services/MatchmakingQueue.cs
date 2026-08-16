namespace ZombieGame.Application.Services;

using System.Collections.Concurrent;
using ZombieGame.Application.Interfaces;

public class MatchmakingQueue : IMatchmakingQueue
{
    private readonly ConcurrentQueue<Guid> _queue = new();
    private readonly ConcurrentDictionary<Guid, byte> _queuedUsers = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _enqueueTimes = new();

    public int Count => _queuedUsers.Count;

    public bool Contains(Guid userId) => _queuedUsers.ContainsKey(userId);

    public bool TryEnqueue(Guid userId)
    {
        if (!_queuedUsers.TryAdd(userId, 0))
            return false;

        _enqueueTimes[userId] = DateTime.UtcNow;
        _queue.Enqueue(userId);
        return true;
    }

    public bool TryRemove(Guid userId)
    {
        if (!_queuedUsers.TryRemove(userId, out _))
            return false;

        _enqueueTimes.TryRemove(userId, out _);
        return true;
    }

    public bool TryDequeueBatch(int count, out List<Guid> userIds)
    {
        userIds = new List<Guid>(count);
        while (userIds.Count < count && _queue.TryDequeue(out var userId))
        {
            if (_queuedUsers.TryRemove(userId, out _))
            {
                _enqueueTimes.TryRemove(userId, out _);
                userIds.Add(userId);
            }
        }

        return userIds.Count == count;
    }

    public void Requeue(IEnumerable<Guid> userIds)
    {
        var now = DateTime.UtcNow;
        foreach (var userId in userIds)
        {
            if (_queuedUsers.TryAdd(userId, 0))
            {
                _enqueueTimes[userId] = now;
                _queue.Enqueue(userId);
            }
        }
    }

    public DateTime? GetOldestEnqueueUtc()
    {
        if (_enqueueTimes.IsEmpty)
            return null;

        var oldest = DateTime.MaxValue;
        foreach (var enqueuedAt in _enqueueTimes.Values)
        {
            if (enqueuedAt < oldest)
                oldest = enqueuedAt;
        }

        return oldest == DateTime.MaxValue ? null : oldest;
    }
}
