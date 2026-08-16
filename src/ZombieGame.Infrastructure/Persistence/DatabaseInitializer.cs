namespace ZombieGame.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZombieGame.Infrastructure.Cards;
using ZombieGame.Infrastructure.Progress;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        await context.Database.MigrateAsync();

        if (!await context.CardDefinitions.AnyAsync())
        {
            context.CardDefinitions.AddRange(CardRegistry.GetSeedData());
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} card definitions.", context.CardDefinitions.Count());
        }
        else
        {
            var existingIds = await context.CardDefinitions.Select(c => c.Id).ToListAsync();
            var missing = CardRegistry.GetSeedData().Where(c => !existingIds.Contains(c.Id)).ToList();
            if (missing.Count > 0)
            {
                context.CardDefinitions.AddRange(missing);
                await context.SaveChangesAsync();
                logger.LogInformation("Added {Count} missing card definitions.", missing.Count);
            }
        }

        var existingAchievementIds = await context.Achievements.Select(a => a.Id).ToListAsync();
        var missingAchievements = AchievementCatalog.GetSeedData()
            .Where(a => !existingAchievementIds.Contains(a.Id))
            .ToList();
        if (missingAchievements.Count > 0)
        {
            context.Achievements.AddRange(missingAchievements);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} achievements.", missingAchievements.Count);
        }
    }
}
