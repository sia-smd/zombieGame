namespace ZombieGame.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure.Persistence;

public class PlayerProfileRepository : IPlayerProfileRepository
{
    private readonly ApplicationDbContext _context;

    public PlayerProfileRepository(ApplicationDbContext context) => _context = context;

    public Task<PlayerProfile?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        _context.PlayerProfiles.FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

    public async Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken = default) =>
        await _context.PlayerProfiles.AddAsync(profile, cancellationToken);

    public void Update(PlayerProfile profile) => _context.PlayerProfiles.Update(profile);
}

public class PlayerSessionRepository : IPlayerSessionRepository
{
    private readonly ApplicationDbContext _context;

    public PlayerSessionRepository(ApplicationDbContext context) => _context = context;

    public Task<PlayerSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _context.PlayerSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public Task<PlayerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default) =>
        _context.PlayerSessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash, cancellationToken);

    public async Task AddAsync(PlayerSession session, CancellationToken cancellationToken = default) =>
        await _context.PlayerSessions.AddAsync(session, cancellationToken);

    public void Update(PlayerSession session) => _context.PlayerSessions.Update(session);
}

public class PlayerDeviceRepository : IPlayerDeviceRepository
{
    private readonly ApplicationDbContext _context;

    public PlayerDeviceRepository(ApplicationDbContext context) => _context = context;

    public Task<PlayerDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.PlayerDevices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<PlayerDevice?> GetByPlayerAndDeviceIdAsync(Guid playerId, string deviceId, CancellationToken cancellationToken = default) =>
        _context.PlayerDevices.FirstOrDefaultAsync(d => d.PlayerId == playerId && d.DeviceId == deviceId, cancellationToken);

    public Task<IReadOnlyList<PlayerDevice>> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        _context.PlayerDevices
            .Where(d => d.PlayerId == playerId)
            .OrderByDescending(d => d.LastLoginAt)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<PlayerDevice>)t.Result, cancellationToken);

    public async Task AddAsync(PlayerDevice device, CancellationToken cancellationToken = default) =>
        await _context.PlayerDevices.AddAsync(device, cancellationToken);

    public void Update(PlayerDevice device) => _context.PlayerDevices.Update(device);
}

public class PlayerLoginLogRepository : IPlayerLoginLogRepository
{
    private readonly ApplicationDbContext _context;

    public PlayerLoginLogRepository(ApplicationDbContext context) => _context = context;

    public async Task AddAsync(PlayerLoginLog log, CancellationToken cancellationToken = default) =>
        await _context.PlayerLoginLogs.AddAsync(log, cancellationToken);
}
