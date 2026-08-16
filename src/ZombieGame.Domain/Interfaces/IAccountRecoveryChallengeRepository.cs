namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;

public interface IAccountRecoveryChallengeRepository
{
    Task AddAsync(AccountRecoveryChallenge challenge, CancellationToken cancellationToken = default);

    Task<AccountRecoveryChallenge?> GetActiveAsync(
        Guid guestPlayerId,
        Guid targetPlayerId,
        AccountRecoveryPurpose purpose,
        CancellationToken cancellationToken = default);

    Task InvalidateActiveAsync(
        Guid guestPlayerId,
        Guid targetPlayerId,
        AccountRecoveryPurpose purpose,
        CancellationToken cancellationToken = default);

    void Update(AccountRecoveryChallenge challenge);
}
