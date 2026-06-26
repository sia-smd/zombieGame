namespace ZombieGame.Infrastructure.Game;

using StackExchange.Redis;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Interfaces;
using Microsoft.Extensions.Options;

public sealed class RedisActiveMatchRegistry : IActiveMatchRegistry
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _setKey;

    public RedisActiveMatchRegistry(IConnectionMultiplexer redis, IOptions<RedisSettings> settings)
    {
        _redis = redis;
        _setKey = $"{settings.Value.KeyPrefix}:active-matches";
    }

    public Task RegisterAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _redis.GetDatabase().SetAddAsync(_setKey, matchId.ToString());

    public async Task UnregisterAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        await _redis.GetDatabase().SetRemoveAsync(_setKey, matchId.ToString());
    }

    public async Task<IReadOnlyList<Guid>> GetActiveMatchIdsAsync(CancellationToken cancellationToken = default)
    {
        var members = await _redis.GetDatabase().SetMembersAsync(_setKey);
        var ids = new List<Guid>(members.Length);
        foreach (var member in members)
        {
            if (Guid.TryParse(member, out var id))
                ids.Add(id);
        }

        return ids;
    }
}
