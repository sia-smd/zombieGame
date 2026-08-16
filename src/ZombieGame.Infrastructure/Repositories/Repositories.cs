namespace ZombieGame.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure.Persistence;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLower();
        return _context.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == normalized,
            cancellationToken);
    }

    public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(u => u.Username == username, cancellationToken);

    public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);

    public void Update(User user) => _context.Users.Update(user);
}

public class MatchRepository : IMatchRepository
{
    private readonly ApplicationDbContext _context;

    public MatchRepository(ApplicationDbContext context) => _context = context;

    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Matches.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<Match?> GetBySessionTokenAsync(string sessionToken, CancellationToken cancellationToken = default) =>
        _context.Matches.FirstOrDefaultAsync(m => m.SessionToken == sessionToken, cancellationToken);

    public Task<Match?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Matches
            .Include(m => m.Players)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<IReadOnlyList<Match>> GetActiveMatchesAsync(CancellationToken cancellationToken = default) =>
        _context.Matches
            .Include(m => m.Players)
            .Where(m => m.Status != Domain.Enums.MatchStatus.Finished)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Match>)t.Result, cancellationToken);

    public async Task<IReadOnlyList<Match>> GetWaitingRoomsNeedingBotFillAsync(
        TimeSpan minAge,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - minAge;
        return await _context.Matches
            .Include(m => m.Players)
            .Where(m =>
                m.Status == Domain.Enums.MatchStatus.Waiting &&
                m.FillWithBots &&
                m.CreatedAt <= cutoff &&
                m.Players.Count < m.MaxPlayers)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Match>> GetOpenWaitingRoomsAsync(CancellationToken cancellationToken = default) =>
        await _context.Matches
            .Include(m => m.Players)
                .ThenInclude(p => p.User)
            .Where(m =>
                m.Status == Domain.Enums.MatchStatus.Waiting &&
                m.Players.Any(p => !p.IsBot) &&
                m.Players.Count < m.MaxPlayers)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Match match, CancellationToken cancellationToken = default) =>
        await _context.Matches.AddAsync(match, cancellationToken);

    public async Task AddPlayerAsync(MatchPlayer player, CancellationToken cancellationToken = default) =>
        await _context.MatchPlayers.AddAsync(player, cancellationToken);

    public void RemovePlayer(MatchPlayer player) => _context.MatchPlayers.Remove(player);

    public void Update(Match match) => _context.Matches.Update(match);
}

public class TransactionRepository : ITransactionRepository
{
    private readonly ApplicationDbContext _context;

    public TransactionRepository(ApplicationDbContext context) => _context = context;

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
        await _context.Transactions.AddAsync(transaction, cancellationToken);

    public Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default) =>
        _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Transaction>)t.Result, cancellationToken);

    public Task<bool> ExistsAsync(
        Guid userId,
        Guid matchId,
        TransactionType type,
        CancellationToken cancellationToken = default) =>
        _context.Transactions.AnyAsync(
            t => t.UserId == userId && t.MatchId == matchId && t.Type == type,
            cancellationToken);
}

public class GameActionLogRepository : IGameActionLogRepository
{
    private readonly ApplicationDbContext _context;

    public GameActionLogRepository(ApplicationDbContext context) => _context = context;

    public Task<bool> ExistsByIdempotencyKeyAsync(Guid matchId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        _context.GameActionLogs.AnyAsync(
            l => l.MatchId == matchId && l.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task AddAsync(GameActionLog log, CancellationToken cancellationToken = default) =>
        await _context.GameActionLogs.AddAsync(log, cancellationToken);

    public Task<IReadOnlyList<GameActionLog>> GetByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _context.GameActionLogs
            .Where(l => l.MatchId == matchId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<GameActionLog>)t.Result, cancellationToken);

    public Task<GameActionLog?> GetLatestWithSnapshotAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _context.GameActionLogs
            .Where(l => l.MatchId == matchId && l.StateSnapshotJson != null)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
