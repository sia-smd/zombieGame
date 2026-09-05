namespace ZombieGame.Application.DTOs.Account;

using ZombieGame.Domain.Enums;

public record RegisterGuestRequest(
    string DeviceId,
    DevicePlatform Platform,
    string AppVersion,
    string? Nickname = null);

public record RegisterGuestResponse(
    Guid PlayerId,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    GuestProfileDto Profile);

public record AccountLoginRequest(
    string PhoneNumber,
    string Password,
    string DeviceId,
    DevicePlatform Platform,
    string AppVersion);

public record AccountLoginResponse(
    Guid PlayerId,
    string Username,
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

public record VerifyMobileRequest(string MobileNumber, string Code);

public record VerifyMobileResponse(bool Success, string Message);

public record ChangePasswordRequest(string NewPassword, string? CurrentPassword = null);

public record ChangePasswordResponse(bool Success, string Message);

public record RefreshTokenRequest(string? RefreshToken = null);

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);

public record LogoutResponse(bool Success, string Message);

public record RecoverAccountSendRequest(string Username, string Password);

public record RecoverAccountSendResponse(bool Success, string Message);

public record RecoverAccountVerifyRequest(
    string Username,
    string Password,
    string Code,
    string DeviceId,
    DevicePlatform Platform,
    string AppVersion);

public record RecoverAccountVerifyResponse(
    Guid PlayerId,
    string Username,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    GuestProfileDto Profile);
