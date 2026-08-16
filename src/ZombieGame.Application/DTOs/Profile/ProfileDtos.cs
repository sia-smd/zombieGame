namespace ZombieGame.Application.DTOs.Profile;

using ZombieGame.Domain.Enums;

public record UpdateProfileRequest(string? Name = null, string? ImageId = null);

public record UpdateUsernameRequest(string Username);

public record UploadAvatarRequest(string ImageBase64);

public record AvatarOptionDto(string Id, string Label);

public record PlayerProfileDto(
    string Name,
    string ImageId,
    int Level);

public record PlayerStatisticsDto(
    int Wins,
    int Losses,
    int MatchesPlayed);

public record PlayerInventoryItemDto(
    string ItemId,
    int Quantity);

public record CurrentPlayerProfileResponse(
    Guid PlayerId,
    AccountType AccountType,
    string Name,
    string Username,
    string ImageId,
    string? CustomAvatarData,
    int Level,
    int Coins,
    string? PhoneNumber,
    bool MobileVerified,
    string? PendingPhoneNumber,
    bool HasPassword,
    IReadOnlyList<PlayerInventoryItemDto> Inventory,
    PlayerStatisticsDto Statistics,
    DateTime CreatedDate);
