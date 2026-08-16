namespace ZombieGame.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure.Persistence;

public sealed class PlayerStatRepository : IPlayerStatRepository
{
    private readonly ApplicationDbContext _context;

    public PlayerStatRepository(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<PlayerStat>> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        await _context.PlayerStats
            .Where(s => s.PlayerId == playerId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlayerStat>> GetByPlayersAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        if (playerIds.Count == 0)
            return [];

        return await _context.PlayerStats
            .Where(s => playerIds.Contains(s.PlayerId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PlayerStat stat, CancellationToken cancellationToken = default) =>
        await _context.PlayerStats.AddAsync(stat, cancellationToken);

    public void Update(PlayerStat stat) => _context.PlayerStats.Update(stat);
}

public sealed class AchievementRepository : IAchievementRepository
{
    private readonly ApplicationDbContext _context;

    public AchievementRepository(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<Achievement>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.Achievements
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Threshold)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Achievement>> GetActiveByStatKeysAsync(
        IReadOnlyCollection<string> statKeys,
        CancellationToken cancellationToken = default)
    {
        if (statKeys.Count == 0)
            return [];

        return await _context.Achievements
            .Where(a => a.IsActive && statKeys.Contains(a.StatKey))
            .ToListAsync(cancellationToken);
    }
}

public sealed class PlayerAchievementRepository : IPlayerAchievementRepository
{
    private readonly ApplicationDbContext _context;

    public PlayerAchievementRepository(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<PlayerAchievement>> GetByPlayerAsync(
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        await _context.PlayerAchievements
            .Include(p => p.Achievement)
            .Where(p => p.PlayerId == playerId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<(Guid PlayerId, Guid AchievementId)>> GetUnlockedPairsAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        if (playerIds.Count == 0)
            return new HashSet<(Guid, Guid)>();

        var rows = await _context.PlayerAchievements
            .Where(p => playerIds.Contains(p.PlayerId))
            .Select(p => new { p.PlayerId, p.AchievementId })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.PlayerId, r.AchievementId)).ToHashSet();
    }

    public async Task AddAsync(PlayerAchievement achievement, CancellationToken cancellationToken = default) =>
        await _context.PlayerAchievements.AddAsync(achievement, cancellationToken);
}
