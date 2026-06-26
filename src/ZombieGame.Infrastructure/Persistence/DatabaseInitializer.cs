namespace ZombieGame.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZombieGame.Infrastructure.Cards;

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
    }
}
