namespace ZombieGame.Infrastructure.Game;

using System.Collections.Concurrent;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public sealed class InMemoryGameSessionStore : IGameSessionStore
{
    private readonly ConcurrentDictionary<Guid, GameSessionState> _sessions = new();
    private readonly ConcurrentDictionary<string, Guid> _tokenIndex = new();

    public Task<GameSessionState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sessions.TryGetValue(matchId, out var state) ? state : null);

    public Task<GameSessionState?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tokenIndex.TryGetValue(sessionToken, out var matchId) ? GetSync(matchId) : null);

    public Task SetAsync(GameSessionState state, CancellationToken cancellationToken = default)
    {
        _sessions[state.MatchId] = state;
        _tokenIndex[state.SessionToken] = state.MatchId;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryRemove(matchId, out var state))
            _tokenIndex.TryRemove(state.SessionToken, out _);
        return Task.CompletedTask;
    }

    public Task<bool> TryAddAsync(Guid matchId, GameSessionState state, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryAdd(matchId, state))
            return Task.FromResult(false);

        _tokenIndex[state.SessionToken] = matchId;
        return Task.FromResult(true);
    }

    private GameSessionState? GetSync(Guid matchId) =>
        _sessions.TryGetValue(matchId, out var state) ? state : null;
}
