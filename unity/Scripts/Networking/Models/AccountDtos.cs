using System;
using System.Collections.Generic;
using ZombieGame.UnityClient.Networking.Models;

namespace ZombieGame.UnityClient.Networking
{
    public sealed record RegisterGuestRequest(string DeviceId, DevicePlatform Platform, string AppVersion);

    public sealed record RegisterGuestResponse(
        Guid PlayerId,
        string AccessToken,
        string RefreshToken,
        DateTime AccessTokenExpiresAt,
        DateTime RefreshTokenExpiresAt,
        GuestProfileDto Profile);

    public sealed record GuestProfileDto(
        string Name,
        string ImageId,
        int Level,
        int Coins,
        int Wins,
        int Losses);

    public sealed record RefreshTokenRequest(string RefreshToken);

    public sealed record RefreshTokenResponse(
        string AccessToken,
        string RefreshToken,
        DateTime AccessTokenExpiresAt,
        DateTime RefreshTokenExpiresAt);

    public sealed record CurrentPlayerProfileResponse(
        Guid PlayerId,
        AccountType AccountType,
        string Name,
        string ImageId,
        int Level,
        int Coins,
        IReadOnlyList<PlayerInventoryItemDto> Inventory,
        PlayerStatisticsDto Statistics,
        DateTime CreatedDate);

    public sealed record PlayerInventoryItemDto(string ItemId, int Quantity);
    public sealed record PlayerStatisticsDto(int Wins, int Losses, int MatchesPlayed);
    public sealed record UpdateProfileRequest(string Name, string ImageId);
}
