namespace ZombieGame.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure.Game;
using ZombieGame.Infrastructure.Repositories;
using ZombieGame.Infrastructure.Security;
using ZombieGame.Infrastructure.Cards;
using ZombieGame.Infrastructure.Persistence;

public static partial class DependencyInjection
{
    public static IServiceCollection AddGameInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var redisSettings = configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>() ?? new RedisSettings();

        if (redisSettings.Enabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisSettings.ConnectionString));
            services.AddSingleton<IGameSessionStore, RedisGameSessionStore>();
            services.AddSingleton<IActiveMatchRegistry, RedisActiveMatchRegistry>();
            services.AddSingleton<RedisMatchLock>();
        }
        else
        {
            services.AddSingleton<IGameSessionStore, InMemoryGameSessionStore>();
            services.AddSingleton<IActiveMatchRegistry, InMemoryActiveMatchRegistry>();
        }

        return services;
    }
}
