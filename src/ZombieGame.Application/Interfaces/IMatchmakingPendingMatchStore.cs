namespace ZombieGame.Application.Interfaces;

public interface IMatchmakingPendingMatchStore
{
    void Set(Guid playerId, Guid matchId, string sessionToken);
    bool TryTake(Guid playerId, out Guid matchId, out string sessionToken);
    void Remove(Guid playerId);
}
