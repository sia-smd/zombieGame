namespace ZombieGame.Application.Interfaces;

public interface IAccountRecoveryService
{
  // Prepared for future implementation — guest accounts remain upgradeable without data loss.

    Task<bool> CanRecoverByMobileAsync(string mobileNumber, CancellationToken cancellationToken = default);

    Task<bool> CanLinkGoogleAccountAsync(string googleSubjectId, CancellationToken cancellationToken = default);

    Task<bool> CanLinkAppleAccountAsync(string appleSubjectId, CancellationToken cancellationToken = default);
}
