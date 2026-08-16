namespace ZombieGame.Application.Interfaces;

public interface IMatchmakingQueue
{
    bool TryEnqueue(Guid userId);
    bool TryRemove(Guid userId);
    bool Contains(Guid userId);
    int Count { get; }
    bool TryDequeueBatch(int count, out List<Guid> userIds);
    void Requeue(IEnumerable<Guid> userIds);
    DateTime? GetOldestEnqueueUtc();
}
