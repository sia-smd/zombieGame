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
            services.AddSingleton<IRoomStateStore, RedisRoomStateStore>();
            services.AddSingleton<IBattlePairHistoryStore, RedisBattlePairHistoryStore>();
            services.AddSingleton<IDiscussionChatStore, RedisDiscussionChatStore>();
            services.AddSingleton<IRoomLock, RedisRoomLockAdapter>();
            services.AddSingleton<IBattleStore, RedisBattleStore>();
            services.AddSingleton<IPlayerActiveMatchStore, RedisPlayerActiveMatchStore>();
            services.AddSingleton<IRoomSnapshotStore, RedisRoomSnapshotStore>();
            services.AddSingleton<IMatchEventLogStore, RedisMatchEventLogStore>();
            services.AddSingleton<IMatchSummaryStore, RedisMatchSummaryStore>();
        }
        else
        {
            services.AddSingleton<IGameSessionStore, InMemoryGameSessionStore>();
            services.AddSingleton<IActiveMatchRegistry, InMemoryActiveMatchRegistry>();
            services.AddSingleton<IRoomStateStore, InMemoryRoomStateStore>();
            services.AddSingleton<IBattlePairHistoryStore, InMemoryBattlePairHistoryStore>();
            services.AddSingleton<IDiscussionChatStore, InMemoryDiscussionChatStore>();
            services.AddSingleton<IBattleStore, InMemoryBattleStore>();
            services.AddSingleton<IPlayerActiveMatchStore, InMemoryPlayerActiveMatchStore>();
            services.AddSingleton<IRoomSnapshotStore, InMemoryRoomSnapshotStore>();
            services.AddSingleton<IMatchEventLogStore, InMemoryMatchEventLogStore>();
            services.AddSingleton<IMatchSummaryStore, InMemoryMatchSummaryStore>();
            services.AddSingleton<IRoomLock, InMemoryRoomLock>();
        }

        return services;
    }
}
