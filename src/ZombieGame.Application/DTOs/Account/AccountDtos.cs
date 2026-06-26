namespace ZombieGame.Application.DTOs.Account;

using ZombieGame.Domain.Enums;

public record RegisterGuestRequest(
    string DeviceId,
    DevicePlatform Platform,
    string AppVersion);

public record RegisterGuestResponse(
    Guid PlayerId,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    GuestProfileDto Profile);

public record GuestProfileDto(
    string Name,
    string ImageId,
    int Level,
    int Coins,
    int Wins,
    int Losses);

public record AddMobileRequest(string MobileNumber);

public record AddMobileResponse(bool Success, bool VerificationRequired, string Message);

public record RefreshTokenRequest(string RefreshToken);

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);

public record LogoutResponse(bool Success, string Message);
