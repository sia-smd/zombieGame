namespace ZombieGame.Infrastructure.Game;

using System.Collections.Concurrent;
using System.Text.Json;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public sealed class InMemoryBattleStore : IBattleStore
{
    private readonly ConcurrentDictionary<(Guid MatchId, Guid BattleId), Battle> _battles = new();

    public Task<Battle?> GetAsync(Guid matchId, Guid battleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_battles.TryGetValue((matchId, battleId), out var b) ? b : null);

    public Task SaveAsync(Battle battle, CancellationToken cancellationToken = default)
    {
        _battles[(battle.MatchId, battle.BattleId)] = battle;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Battle>> GetByMatchAndDayAsync(Guid matchId, int dayNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Battle>>(_battles.Values
            .Where(b => b.MatchId == matchId && b.DayNumber == dayNumber)
            .ToList());

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        foreach (var key in _battles.Keys.Where(k => k.MatchId == matchId).ToList())
            _battles.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPlayerActiveMatchStore : IPlayerActiveMatchStore
{
    private readonly ConcurrentDictionary<Guid, PlayerActiveMatch> _entries = new();

    public Task<PlayerActiveMatch?> GetAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_entries.TryGetValue(playerId, out var e) ? e : null);

    public Task SetAsync(PlayerActiveMatch entry, CancellationToken cancellationToken = default)
    {
        entry.LastSeen = DateTime.UtcNow;
        _entries[entry.PlayerId] = entry;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(playerId, out _);
        return Task.CompletedTask;
    }

    public Task TouchAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(playerId, out var entry))
            entry.LastSeen = DateTime.UtcNow;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryRoomSnapshotStore : IRoomSnapshotStore
{
    private readonly ConcurrentDictionary<Guid, List<RoomSnapshot>> _snapshots = new();

    public Task AppendAsync(RoomSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var list = _snapshots.GetOrAdd(snapshot.MatchId, _ => new List<RoomSnapshot>());
        lock (list)
            list.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<RoomSnapshot?> GetLatestAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (!_snapshots.TryGetValue(matchId, out var list))
            return Task.FromResult<RoomSnapshot?>(null);
        lock (list)
            return Task.FromResult(list.OrderByDescending(s => s.CreatedAt).FirstOrDefault());
    }

    public Task<IReadOnlyList<RoomSnapshot>> GetAllAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (!_snapshots.TryGetValue(matchId, out var list))
            return Task.FromResult<IReadOnlyList<RoomSnapshot>>([]);
        lock (list)
            return Task.FromResult<IReadOnlyList<RoomSnapshot>>(list.ToList());
    }

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        _snapshots.TryRemove(matchId, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryMatchEventLogStore : IMatchEventLogStore
{
    private readonly ConcurrentDictionary<Guid, List<MatchEventEntry>> _logs = new();
    private readonly ConcurrentDictionary<Guid, int> _sequences = new();

    public Task AppendAsync(MatchEventEntry entry, CancellationToken cancellationToken = default)
    {
        var seq = _sequences.AddOrUpdate(entry.MatchId, 1, (_, n) => n + 1);
        entry.SequenceNumber = seq;
        var list = _logs.GetOrAdd(entry.MatchId, _ => new List<MatchEventEntry>());
        lock (list)
            list.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MatchEventEntry>> GetAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (!_logs.TryGetValue(matchId, out var list))
            return Task.FromResult<IReadOnlyList<MatchEventEntry>>([]);
        lock (list)
            return Task.FromResult<IReadOnlyList<MatchEventEntry>>(list.OrderBy(e => e.SequenceNumber).ToList());
    }

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        _logs.TryRemove(matchId, out _);
        _sequences.TryRemove(matchId, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryMatchSummaryStore : IMatchSummaryStore
{
    private readonly ConcurrentDictionary<Guid, MatchSummary> _summaries = new();

    public Task SaveAsync(MatchSummary summary, CancellationToken cancellationToken = default)
    {
        _summaries[summary.MatchId] = summary;
        return Task.CompletedTask;
    }

    public Task<MatchSummary?> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_summaries.TryGetValue(matchId, out var s) ? s : null);
}
