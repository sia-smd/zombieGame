namespace ZombieGame.Infrastructure.Game;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZombieGame.Application.Game;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public sealed class RedisGameSessionStore : IGameSessionStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisGameSessionStore> _logger;

    public RedisGameSessionStore(
        IConnectionMultiplexer redis,
        IOptions<RedisSettings> settings,
        ILogger<RedisGameSessionStore> logger)
    {
        _redis = redis;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<GameSessionState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var payload = await db.StringGetAsync(StateKey(matchId));
        if (payload.IsNullOrEmpty)
            return null;

        try
        {
            return GameSessionStateSerializer.Deserialize(payload!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize session state for match {MatchId}", matchId);
            return null;
        }
    }

    public async Task<GameSessionState?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var matchIdValue = await db.StringGetAsync(TokenKey(sessionToken));
        if (matchIdValue.IsNullOrEmpty || !Guid.TryParse(matchIdValue, out var matchId))
            return null;

        return await GetAsync(matchId, cancellationToken);
    }

    public async Task SetAsync(GameSessionState state, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var json = GameSessionStateSerializer.Serialize(state);
        var ttl = TimeSpan.FromHours(_settings.SessionTtlHours);

        var batch = db.CreateBatch();
        var stateTask = batch.StringSetAsync(StateKey(state.MatchId), json, ttl);
        var tokenTask = batch.StringSetAsync(TokenKey(state.SessionToken), state.MatchId.ToString(), ttl);
        batch.Execute();
        await Task.WhenAll(stateTask, tokenTask);
    }

    public async Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var state = await GetAsync(matchId, cancellationToken);
        var db = _redis.GetDatabase();
        if (state is not null)
            await db.KeyDeleteAsync(TokenKey(state.SessionToken));

        await db.KeyDeleteAsync(StateKey(matchId));
    }

    public async Task<bool> TryAddAsync(Guid matchId, GameSessionState state, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var added = await db.StringSetAsync(
            StateKey(matchId),
            GameSessionStateSerializer.Serialize(state),
            TimeSpan.FromHours(_settings.SessionTtlHours),
            When.NotExists);

        if (!added)
            return false;

        await db.StringSetAsync(
            TokenKey(state.SessionToken),
            matchId.ToString(),
            TimeSpan.FromHours(_settings.SessionTtlHours));

        return true;
    }

    private string StateKey(Guid matchId) => $"{_settings.KeyPrefix}:match:{matchId}:state";

    private string TokenKey(string sessionToken) => $"{_settings.KeyPrefix}:match:token:{sessionToken}";
}
