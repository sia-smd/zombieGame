namespace ZombieGame.Infrastructure.Game;

using System.Collections.Concurrent;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public sealed class InMemoryRoomStateStore : IRoomStateStore
{
    private readonly ConcurrentDictionary<Guid, RoomState> _rooms = new();

    public Task<RoomState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rooms.TryGetValue(matchId, out var state) ? state : null);

    public Task SetAsync(RoomState state, CancellationToken cancellationToken = default)
    {
        state.SnapshotVersion++;
        _rooms[state.MatchId] = state;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        _rooms.TryRemove(matchId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> TryAddAsync(Guid matchId, RoomState state, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rooms.TryAdd(matchId, state));
}

public sealed class InMemoryBattlePairHistoryStore : IBattlePairHistoryStore
{
    private readonly ConcurrentDictionary<(Guid MatchId, Guid PlayerId), HashSet<Guid>> _history = new();

    public Task<IReadOnlySet<Guid>> GetPreviousOpponentsAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default)
    {
        _history.TryGetValue((matchId, playerId), out var set);
        return Task.FromResult<IReadOnlySet<Guid>>(set ?? new HashSet<Guid>());
    }

    public Task RecordPairAsync(Guid matchId, Guid player1Id, Guid player2Id, CancellationToken cancellationToken = default)
    {
        RecordOne(matchId, player1Id, player2Id);
        RecordOne(matchId, player2Id, player1Id);
        return Task.CompletedTask;
    }

    public Task ResetPlayerHistoryAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default)
    {
        _history.TryRemove((matchId, playerId), out _);
        return Task.CompletedTask;
    }

    public Task ResetAllAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        foreach (var key in _history.Keys.Where(k => k.MatchId == matchId).ToList())
            _history.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        ResetAllAsync(matchId, cancellationToken);

    private void RecordOne(Guid matchId, Guid playerId, Guid opponentId)
    {
        var set = _history.GetOrAdd((matchId, playerId), _ => new HashSet<Guid>());
        lock (set)
            set.Add(opponentId);
    }
}

public sealed class InMemoryDiscussionChatStore : IDiscussionChatStore
{
    private readonly ConcurrentDictionary<Guid, List<ChatMessage>> _messages = new();

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (!_messages.TryGetValue(matchId, out var list))
            return Task.FromResult<IReadOnlyList<ChatMessage>>([]);

        lock (list)
            return Task.FromResult<IReadOnlyList<ChatMessage>>(list.ToList());
    }

    public Task AddMessageAsync(Guid matchId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        var list = _messages.GetOrAdd(matchId, _ => new List<ChatMessage>());
        lock (list)
            list.Add(message);
        return Task.CompletedTask;
    }

    public Task ClearAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        _messages.TryRemove(matchId, out _);
        return Task.CompletedTask;
    }

    public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        ClearAsync(matchId, cancellationToken);
}

public sealed class InMemoryRoomLock : IRoomLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(matchId, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(TimeSpan.Zero, cancellationToken))
            return null;

        return new Handle(semaphore);
    }

    private sealed class Handle(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
