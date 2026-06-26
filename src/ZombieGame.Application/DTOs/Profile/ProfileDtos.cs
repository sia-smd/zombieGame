namespace ZombieGame.Application.DTOs.Profile;

using ZombieGame.Domain.Enums;

public record UpdateProfileRequest(string Name, string ImageId);

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
    string ImageId,
    int Level,
    int Coins,
    IReadOnlyList<PlayerInventoryItemDto> Inventory,
    PlayerStatisticsDto Statistics,
    DateTime CreatedDate);
