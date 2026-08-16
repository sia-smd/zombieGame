namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Entities;

public interface IPlayerStatRepository
{
    Task<IReadOnlyList<PlayerStat>> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerStat>> GetByPlayersAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);
    Task AddAsync(PlayerStat stat, CancellationToken cancellationToken = default);
    void Update(PlayerStat stat);
}

public interface IAchievementRepository
{
    Task<IReadOnlyList<Achievement>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Achievement>> GetActiveByStatKeysAsync(
        IReadOnlyCollection<string> statKeys,
        CancellationToken cancellationToken = default);
}

public interface IPlayerAchievementRepository
{
    Task<IReadOnlyList<PlayerAchievement>> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<(Guid PlayerId, Guid AchievementId)>> GetUnlockedPairsAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default);
    Task AddAsync(PlayerAchievement achievement, CancellationToken cancellationToken = default);
}
