namespace ZombieGame.Application.Services;

using System.Collections.Concurrent;
using ZombieGame.Application.Interfaces;

public sealed class MatchmakingPendingMatchStore : IMatchmakingPendingMatchStore
{
    private readonly ConcurrentDictionary<Guid, PendingMatch> _pending = new();

    public void Set(Guid playerId, Guid matchId, string sessionToken) =>
        _pending[playerId] = new PendingMatch(matchId, sessionToken);

    public bool TryTake(Guid playerId, out Guid matchId, out string sessionToken)
    {
        if (_pending.TryRemove(playerId, out var pending))
        {
            matchId = pending.MatchId;
            sessionToken = pending.SessionToken;
            return true;
        }

        matchId = default;
        sessionToken = string.Empty;
        return false;
    }

    public void Remove(Guid playerId) => _pending.TryRemove(playerId, out _);

    private sealed record PendingMatch(Guid MatchId, string SessionToken);
}
