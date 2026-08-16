namespace ZombieGame.Infrastructure.Game;

using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public sealed class RedisBattleStore : IBattleStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RedisBattleStore(IConnectionMultiplexer redis, IOptions<RedisSettings> settings) =>
        (_redis, _settings) = (redis, settings.Value);

    public async Task<Battle?> GetAsync(Guid matchId, Guid battleId, CancellationToken cancellationToken = default)
    {
        var payload = await _redis.GetDatabase().StringGetAsync(BattleKey(matchId, battleId));
        return payload.IsNullOrEmpty ? null : JsonSerializer.Deserialize<Battle>(payload!, JsonOptions);
    }

    public async Task SaveAsync(Battle battle, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var ttl = TimeSpan.FromHours(_settings.SessionTtlHours);
        var key = BattleKey(battle.MatchId, battle.BattleId);
        await db.StringSetAsync(key, JsonSerializer.Serialize(battle, JsonOptions), ttl);
        await db.SetAddAsync(DayBattlesKey(battle.MatchId, battle.DayNumber), battle.BattleId.ToString());
        await db.KeyExpireAsync(DayBattlesKey(battle.MatchId, battle.DayNumber), ttl);
    }

    public async Task<IReadOnlyList<Battle>> GetByMatchAndDayAsync(Guid matchId, int dayNumber, CancellationToken cancellationToken = default)
    {
        var ids = await _redis.GetDatabase().SetMembersAsync(DayBattlesKey(matchId, dayNumber));
        var battles = new List<Battle>();
        foreach (var id in ids)
        {
            if (Guid.TryParse(id, out var battleId))
            {
                var battle = await GetAsync(matchId, battleId, cancellationToken);
                if (battle is not null)
                    battles.Add(battle);
            }
        }
        return battles;
    }

    public async Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"{_settings.KeyPrefix}:room:{matchId}:battle:*";
        foreach (var key in server.Keys(pattern: pattern))
            await _redis.GetDatabase().KeyDeleteAsync(key);
    }

    private string BattleKey(Guid matchId, Guid battleId) => $"{_settings.KeyPrefix}:room:{matchId}:battle:{battleId}";
    private string DayBattlesKey(Guid matchId, int day) => $"{_settings.KeyPrefix}:room:{matchId}:battles:day:{day}";
}

public sealed class RedisPlayerActiveMatchStore : IPlayerActiveMatchStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RedisPlayerActiveMatchStore(IConnectionMultiplexer redis, IOptions<RedisSettings> settings) =>
        (_redis, _settings) = (redis, settings.Value);

    public async Task<PlayerActiveMatch?> GetAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var payload = await _redis.GetDatabase().StringGetAsync(Key(playerId));
        return payload.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PlayerActiveMatch>(payload!, JsonOptions);
    }

    public async Task SetAsync(PlayerActiveMatch entry, CancellationToken cancellationToken = default)
    {
        entry.LastSeen = DateTime.UtcNow;
        await _redis.GetDatabase().StringSetAsync(
            Key(entry.PlayerId),
            JsonSerializer.Serialize(entry, JsonOptions),
            TimeSpan.FromHours(_settings.SessionTtlHours));
    }

    public Task RemoveAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        _redis.GetDatabase().KeyDeleteAsync(Key(playerId));

    public async Task TouchAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var entry = await GetAsync(playerId, cancellationToken);
        if (entry is not null)
            await SetAsync(entry, cancellationToken);
    }

    private string Key(Guid playerId) => $"{_settings.KeyPrefix}:player:{playerId}:active";
}

public sealed class RedisRoomSnapshotStore : IRoomSnapshotStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;

    public RedisRoomSnapshotStore(IConnectionMultiplexer redis, IOptions<RedisSettings> settings) =>
        (_redis, _settings) = (redis, settings.Value);

    public async Task AppendAsync(RoomSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var key = Key(snapshot.MatchId);
        var db = _redis.GetDatabase();
        await db.ListRightPushAsync(key, JsonSerializer.Serialize(snapshot));
        await db.KeyExpireAsync(key, TimeSpan.FromHours(_settings.SessionTtlHours));
    }

    public async Task<RoomSnapshot?> GetLatestAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var value = await _redis.GetDatabase().ListGetByIndexAsync(Key(matchId), -1);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<RoomSnapshot>((string)value!);
    }

    public async Task<IReadOnlyList<RoomSnapshot>> GetAllAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var values = await _redis.GetDatabase().ListRangeAsync(Key(matchId));
        return values.Select(v => JsonSerializer.Deserialize<RoomSnapshot>((string)v!)!)
            .Where(s => s is not null)
            .ToList();
    }

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _redis.GetDatabase().KeyDeleteAsync(Key(matchId));

    private string Key(Guid matchId) => $"{_settings.KeyPrefix}:room:{matchId}:snapshots";
}

public sealed class RedisMatchEventLogStore : IMatchEventLogStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;

    public RedisMatchEventLogStore(IConnectionMultiplexer redis, IOptions<RedisSettings> settings) =>
        (_redis, _settings) = (redis, settings.Value);

    public async Task AppendAsync(MatchEventEntry entry, CancellationToken cancellationToken = default)
    {
        var key = Key(entry.MatchId);
        var db = _redis.GetDatabase();
        entry.SequenceNumber = (int)await db.ListLengthAsync(key) + 1;
        await db.ListRightPushAsync(key, JsonSerializer.Serialize(entry));
        await db.KeyExpireAsync(key, TimeSpan.FromHours(_settings.SessionTtlHours));
    }

    public async Task<IReadOnlyList<MatchEventEntry>> GetAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var values = await _redis.GetDatabase().ListRangeAsync(Key(matchId));
        return values.Select(v => JsonSerializer.Deserialize<MatchEventEntry>((string)v!)!)
            .OrderBy(e => e.SequenceNumber)
            .ToList();
    }

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _redis.GetDatabase().KeyDeleteAsync(Key(matchId));

    private string Key(Guid matchId) => $"{_settings.KeyPrefix}:room:{matchId}:events";
}

public sealed class RedisMatchSummaryStore : IMatchSummaryStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;

    public RedisMatchSummaryStore(IConnectionMultiplexer redis, IOptions<RedisSettings> settings) =>
        (_redis, _settings) = (redis, settings.Value);

    public Task SaveAsync(MatchSummary summary, CancellationToken cancellationToken = default) =>
        _redis.GetDatabase().StringSetAsync(
            $"{_settings.KeyPrefix}:room:{summary.MatchId}:summary",
            JsonSerializer.Serialize(summary),
            TimeSpan.FromDays(30));

    public async Task<MatchSummary?> GetAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var payload = await _redis.GetDatabase().StringGetAsync($"{_settings.KeyPrefix}:room:{matchId}:summary");
        return payload.IsNullOrEmpty ? null : JsonSerializer.Deserialize<MatchSummary>(payload!);
    }
}
