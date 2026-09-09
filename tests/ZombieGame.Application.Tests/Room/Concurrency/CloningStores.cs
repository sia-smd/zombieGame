namespace ZombieGame.Application.Tests.Room.Concurrency;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZombieGame.Application.Room;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

/// <summary>
/// Redis-faithful copies: Get returns a deserialized clone so overlapping writers can lose updates.
/// </summary>
public sealed class CloningRoomStateStore : IRoomStateStore
{
    private readonly ConcurrentDictionary<Guid, string> _rooms = new();

    public Task<RoomState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rooms.TryGetValue(matchId, out var json) ? RoomStateSerializer.Deserialize(json) : null);

    public Task SetAsync(RoomState state, CancellationToken cancellationToken = default)
    {
        state.SnapshotVersion++;
        _rooms[state.MatchId] = RoomStateSerializer.Serialize(state);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        _rooms.TryRemove(matchId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> TryAddAsync(Guid matchId, RoomState state, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rooms.TryAdd(matchId, RoomStateSerializer.Serialize(state)));
}

public sealed class CloningBattleStore : IBattleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ConcurrentDictionary<(Guid MatchId, Guid BattleId), string> _battles = new();

    public Task<Battle?> GetAsync(Guid matchId, Guid battleId, CancellationToken cancellationToken = default)
    {
        if (!_battles.TryGetValue((matchId, battleId), out var json))
            return Task.FromResult<Battle?>(null);
        return Task.FromResult(JsonSerializer.Deserialize<Battle>(json, JsonOptions));
    }

    public Task SaveAsync(Battle battle, CancellationToken cancellationToken = default)
    {
        _battles[(battle.MatchId, battle.BattleId)] = JsonSerializer.Serialize(battle, JsonOptions);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Battle>> GetByMatchAndDayAsync(Guid matchId, int dayNumber, CancellationToken cancellationToken = default)
    {
        var list = _battles.Values
            .Select(json => JsonSerializer.Deserialize<Battle>(json, JsonOptions)!)
            .Where(b => b.MatchId == matchId && b.DayNumber == dayNumber)
            .ToList();
        return Task.FromResult<IReadOnlyList<Battle>>(list);
    }

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        foreach (var key in _battles.Keys.Where(k => k.MatchId == matchId).ToList())
            _battles.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Forces two Get callers to overlap before either Save, reproducing Redis last-write-wins.
/// </summary>
public sealed class GatedBattleStore : IBattleStore
{
    private readonly IBattleStore _inner;
    private readonly int _needed;
    private int _arrivals;
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GatedBattleStore(IBattleStore inner, int participants)
    {
        _inner = inner;
        _needed = participants;
    }

    public async Task<Battle?> GetAsync(Guid matchId, Guid battleId, CancellationToken cancellationToken = default)
    {
        var battle = await _inner.GetAsync(matchId, battleId, cancellationToken);
        if (Interlocked.Increment(ref _arrivals) >= _needed)
            _released.TrySetResult();
        await _released.Task.WaitAsync(cancellationToken);
        return battle;
    }

    public Task SaveAsync(Battle battle, CancellationToken cancellationToken = default) =>
        _inner.SaveAsync(battle, cancellationToken);

    public Task<IReadOnlyList<Battle>> GetByMatchAndDayAsync(Guid matchId, int dayNumber, CancellationToken cancellationToken = default) =>
        _inner.GetByMatchAndDayAsync(matchId, dayNumber, cancellationToken);

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _inner.RemoveMatchAsync(matchId, cancellationToken);
}

public sealed class HeldRoomLock : IRoomLock
{
    private readonly TaskCompletionSource<IAsyncDisposable?> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ReleaseAsync()
    {
        _release.TrySetResult(new NoopHandle());
        return Task.CompletedTask;
    }

    public Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        _release.Task.WaitAsync(cancellationToken);

    private sealed class NoopHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
