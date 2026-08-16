namespace ZombieGame.Application.DTOs.Profile;

public record PlayerProgressResponse(
    IReadOnlyDictionary<string, int> Stats,
    IReadOnlyList<AchievementProgressDto> Achievements);

public record AchievementProgressDto(
    string Code,
    string Title,
    string Description,
    string StatKey,
    int Threshold,
    int Tier,
    int CurrentValue,
    bool Unlocked,
    DateTime? UnlockedAt);
