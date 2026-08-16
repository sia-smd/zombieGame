namespace ZombieGame.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure.Persistence;

public class AccountRecoveryChallengeRepository : IAccountRecoveryChallengeRepository
{
    private readonly ApplicationDbContext _context;

    public AccountRecoveryChallengeRepository(ApplicationDbContext context) => _context = context;

    public async Task AddAsync(AccountRecoveryChallenge challenge, CancellationToken cancellationToken = default) =>
        await _context.AccountRecoveryChallenges.AddAsync(challenge, cancellationToken);

    public Task<AccountRecoveryChallenge?> GetActiveAsync(
        Guid guestPlayerId,
        Guid targetPlayerId,
        AccountRecoveryPurpose purpose,
        CancellationToken cancellationToken = default) =>
        _context.AccountRecoveryChallenges
            .Where(c =>
                c.GuestPlayerId == guestPlayerId
                && c.TargetPlayerId == targetPlayerId
                && c.Purpose == purpose
                && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InvalidateActiveAsync(
        Guid guestPlayerId,
        Guid targetPlayerId,
        AccountRecoveryPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var active = await _context.AccountRecoveryChallenges
            .Where(c =>
                c.GuestPlayerId == guestPlayerId
                && c.TargetPlayerId == targetPlayerId
                && c.Purpose == purpose
                && c.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var challenge in active)
            challenge.ConsumedAt = now;
    }

    public void Update(AccountRecoveryChallenge challenge) =>
        _context.AccountRecoveryChallenges.Update(challenge);
}
