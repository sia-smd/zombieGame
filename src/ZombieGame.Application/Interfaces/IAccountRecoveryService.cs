namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.DTOs.Account;

public interface IAccountRecoveryService
{
    Task<bool> CanRecoverByMobileAsync(string mobileNumber, CancellationToken cancellationToken = default);

    Task<bool> CanLinkGoogleAccountAsync(string googleSubjectId, CancellationToken cancellationToken = default);

    Task<bool> CanLinkAppleAccountAsync(string appleSubjectId, CancellationToken cancellationToken = default);

    Task<RecoverAccountSendResponse> SendRecoverAccountOtpAsync(
        Guid guestPlayerId,
        RecoverAccountSendRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<RecoverAccountVerifyResponse> VerifyRecoverAccountOtpAsync(
        Guid guestPlayerId,
        Guid? guestSessionId,
        RecoverAccountVerifyRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
