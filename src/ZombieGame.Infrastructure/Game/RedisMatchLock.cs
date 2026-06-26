namespace ZombieGame.Infrastructure.Game;

using StackExchange.Redis;
using ZombieGame.Application.Options;
using Microsoft.Extensions.Options;

public sealed class RedisMatchLock
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;

    public RedisMatchLock(IConnectionMultiplexer redis, IOptions<RedisSettings> settings)
    {
        _redis = redis;
        _prefix = settings.Value.KeyPrefix;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var key = $"{_prefix}:match:{matchId}:lock";
        var acquired = await _redis.GetDatabase().StringSetAsync(key, token, ttl, When.NotExists);
        return acquired ? new LockHandle(_redis, key, token) : null;
    }

    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly string _key;
        private readonly string _token;
        private int _released;

        public LockHandle(IConnectionMultiplexer redis, string key, string token)
        {
            _redis = redis;
            _key = key;
            _token = token;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                return;

            const string script = """
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end
                """;

            await _redis.GetDatabase().ScriptEvaluateAsync(script, [_key], [_token]);
        }
    }
}
