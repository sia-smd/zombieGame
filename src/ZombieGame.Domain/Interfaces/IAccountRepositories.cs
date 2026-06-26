namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Entities;

public interface IPlayerProfileRepository
{
    Task<PlayerProfile?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);
    Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken = default);
    void Update(PlayerProfile profile);
}

public interface IPlayerSessionRepository
{
    Task<PlayerSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<PlayerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default);
    Task AddAsync(PlayerSession session, CancellationToken cancellationToken = default);
    void Update(PlayerSession session);
}

public interface IPlayerDeviceRepository
{
    Task<PlayerDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlayerDevice?> GetByPlayerAndDeviceIdAsync(Guid playerId, string deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerDevice>> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);
    Task AddAsync(PlayerDevice device, CancellationToken cancellationToken = default);
    void Update(PlayerDevice device);
}

public interface IPlayerLoginLogRepository
{
    Task AddAsync(PlayerLoginLog log, CancellationToken cancellationToken = default);
}
