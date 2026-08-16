namespace ZombieGame.Infrastructure.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pong = await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis ping {pong.TotalMilliseconds:0}ms");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.", exception);
        }
    }
}
