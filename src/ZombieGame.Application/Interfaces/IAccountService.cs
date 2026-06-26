namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.DTOs.Account;

public interface IAccountService
{
    Task<RegisterGuestResponse> RegisterGuestAsync(
        RegisterGuestRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AddMobileResponse> AddMobileAsync(
        Guid playerId,
        AddMobileRequest request,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<LogoutResponse> LogoutAsync(
        Guid playerId,
        Guid? sessionId,
        string? ipAddress,
        string? deviceId,
        CancellationToken cancellationToken = default);
}
