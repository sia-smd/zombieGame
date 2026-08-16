namespace ZombieGame.Infrastructure.Game;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public sealed class RedisRoomStateStore : IRoomStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisRoomStateStore> _logger;

    public RedisRoomStateStore(
        IConnectionMultiplexer redis,
        IOptions<RedisSettings> settings,
        ILogger<RedisRoomStateStore> logger)
    {
        _redis = redis;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RoomState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var payload = await _redis.GetDatabase().StringGetAsync(CoreKey(matchId));
        if (payload.IsNullOrEmpty)
            return null;

        try
        {
            return RoomStateSerializer.Deserialize(payload!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize room state for {MatchId}", matchId);
            return null;
        }
    }

    public async Task SetAsync(RoomState state, CancellationToken cancellationToken = default)
    {
        state.SnapshotVersion++;
        var db = _redis.GetDatabase();
        var ttl = TimeSpan.FromHours(_settings.SessionTtlHours);
        await db.StringSetAsync(CoreKey(state.MatchId), RoomStateSerializer.Serialize(state), ttl);
        await db.StringSetAsync(TokenKey(state.SessionToken), state.MatchId.ToString(), ttl);
    }

    public async Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var state = await GetAsync(matchId, cancellationToken);
        var db = _redis.GetDatabase();
        if (state is not null)
            await db.KeyDeleteAsync(TokenKey(state.SessionToken));
        await db.KeyDeleteAsync(CoreKey(matchId));
    }

    public async Task<bool> TryAddAsync(Guid matchId, RoomState state, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var ttl = TimeSpan.FromHours(_settings.SessionTtlHours);
        var added = await db.StringSetAsync(CoreKey(matchId), RoomStateSerializer.Serialize(state), ttl, When.NotExists);
        if (!added)
            return false;

        await db.StringSetAsync(TokenKey(state.SessionToken), matchId.ToString(), ttl);
        return true;
    }

    private string CoreKey(Guid matchId) => $"{_settings.KeyPrefix}:room:{matchId}:core";
    private string TokenKey(string token) => $"{_settings.KeyPrefix}:room:token:{token}";
}

public sealed class RedisBattlePairHistoryStore : IBattlePairHistoryStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;

    public RedisBattlePairHistoryStore(IConnectionMultiplexer redis, IOptions<RedisSettings> settings) =>
        (_redis, _settings) = (redis, settings.Value);

    public async Task<IReadOnlySet<Guid>> GetPreviousOpponentsAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default)
    {
        var members = await _redis.GetDatabase().SetMembersAsync(HistoryKey(matchId, playerId));
        return members
            .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
    }

    public async Task RecordPairAsync(Guid matchId, Guid player1Id, Guid player2Id, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var ttl = TimeSpan.FromHours(_settings.SessionTtlHours);
        await db.SetAddAsync(HistoryKey(matchId, player1Id), player2Id.ToString());
        await db.SetAddAsync(HistoryKey(matchId, player2Id), player1Id.ToString());
        await db.KeyExpireAsync(HistoryKey(matchId, player1Id), ttl);
        await db.KeyExpireAsync(HistoryKey(matchId, player2Id), ttl);
    }

    public Task ResetPlayerHistoryAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) =>
        _redis.GetDatabase().KeyDeleteAsync(HistoryKey(matchId, playerId));

    public async Task ResetAllAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"{_settings.KeyPrefix}:room:{matchId}:history:*";
        foreach (var key in server.Keys(pattern: pattern))
            await _redis.GetDatabase().KeyDeleteAsync(key);
    }

    public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        ResetAllAsync(matchId, cancellationToken);

    private string HistoryKey(Guid matchId, Guid playerId) =>
        $"{_settings.KeyPrefix}:room:{matchId}:history:{playerId}";
}

public sealed class RedisDiscussionChatStore : IDiscussionChatStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;

    public RedisDiscussionChatStore(IConnectionMultiplexer redis, IOptions<RedisSettings> settings) =>
        (_redis, _settings) = (redis, settings.Value);

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var values = await _redis.GetDatabase().ListRangeAsync(ChatKey(matchId));
        return values
            .Select(v => System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(v!))
            .Where(m => m is not null)
            .Cast<ChatMessage>()
            .ToList();
    }

    public async Task AddMessageAsync(Guid matchId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = ChatKey(matchId);
        await db.ListRightPushAsync(key, System.Text.Json.JsonSerializer.Serialize(message));
        await db.KeyExpireAsync(key, TimeSpan.FromHours(_settings.SessionTtlHours));
    }

    public Task ClearAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _redis.GetDatabase().KeyDeleteAsync(ChatKey(matchId));

    public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        ClearAsync(matchId, cancellationToken);

    private string ChatKey(Guid matchId) => $"{_settings.KeyPrefix}:room:{matchId}:chat";
}

public sealed class RedisRoomLockAdapter : IRoomLock
{
    private readonly RedisMatchLock _lock;

    public RedisRoomLockAdapter(RedisMatchLock redisMatchLock) => _lock = redisMatchLock;

    public Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        _lock.TryAcquireAsync(matchId, ttl, cancellationToken);
}
