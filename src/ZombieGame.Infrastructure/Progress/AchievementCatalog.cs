namespace ZombieGame.Infrastructure.Progress;

using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Progress;

public static class AchievementCatalog
{
    public static IReadOnlyList<Achievement> GetSeedData() =>
    [
        Medal("aaaaaaaa-0001-4000-8000-000000000001", "wins_1", "اولین پیروزی", "یک بازی را ببر.", PlayerStatKeys.Wins, 1, 1, 10),
        Medal("aaaaaaaa-0001-4000-8000-000000000002", "wins_10", "ده پیروزی", "۱۰ بازی را ببر.", PlayerStatKeys.Wins, 10, 2, 11),
        Medal("aaaaaaaa-0001-4000-8000-000000000003", "wins_50", "پنجاه پیروزی", "۵۰ بازی را ببر.", PlayerStatKeys.Wins, 50, 3, 12),
        Medal("aaaaaaaa-0002-4000-8000-000000000001", "heals_1", "اولین درمان", "یک زامبی را درمان کن.", PlayerStatKeys.Heals, 1, 1, 20),
        Medal("aaaaaaaa-0002-4000-8000-000000000002", "heals_10", "ده درمان", "۱۰ بار درمان موفق انجام بده.", PlayerStatKeys.Heals, 10, 2, 21),
        Medal("aaaaaaaa-0002-4000-8000-000000000003", "heals_50", "پنجاه درمان", "۵۰ بار درمان موفق انجام بده.", PlayerStatKeys.Heals, 50, 3, 22),
        Medal("aaaaaaaa-0003-4000-8000-000000000001", "poisons_1", "اولین آلودگی", "یک بازیکن را آلوده کن.", PlayerStatKeys.Poisons, 1, 1, 30),
        Medal("aaaaaaaa-0003-4000-8000-000000000002", "poisons_10", "ده آلودگی", "۱۰ بار آلودگی موفق انجام بده.", PlayerStatKeys.Poisons, 10, 2, 31),
        Medal("aaaaaaaa-0003-4000-8000-000000000003", "poisons_50", "پنجاه آلودگی", "۵۰ بار آلودگی موفق انجام بده.", PlayerStatKeys.Poisons, 50, 3, 32),
    ];

    private static Achievement Medal(
        string id,
        string code,
        string title,
        string description,
        string statKey,
        int threshold,
        int tier,
        int sortOrder) =>
        new()
        {
            Id = Guid.Parse(id),
            Code = code,
            Title = title,
            Description = description,
            StatKey = statKey,
            Threshold = threshold,
            Tier = tier,
            IsActive = true,
            SortOrder = sortOrder
        };
}
