namespace ZombieGame.Application;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZombieGame.Application.Bots;
using ZombieGame.Application.Game;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Services;
using ZombieGame.Application.Simulation;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GameSettings>(configuration.GetSection(GameSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<AccountSettings>(configuration.GetSection(AccountSettings.SectionName));
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));

        services.AddSingleton<IMatchmakingQueue, MatchmakingQueue>();
        services.AddScoped<IMatchmakingService, MatchmakingService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IPlayerProfileService, PlayerProfileService>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddSingleton<ISmsService, MockSmsService>();
        services.AddSingleton<IForbiddenWordsService, ForbiddenWordsService>();
        services.AddSingleton<IAvatarCatalogService, AvatarCatalogService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<ICoinService, CoinService>();
        services.AddScoped<IBotService, BotService>();

        services.AddSingleton<IDayEventModifier, NormalDayModifier>();
        services.AddSingleton<IDayEventModifier, SunnyDayModifier>();
        services.AddSingleton<IDayEventModifier, StormDayModifier>();
        services.AddSingleton<IDayEventService, DayEventService>();

        services.AddScoped<InfectionTransformationService>();

        services.AddScoped<ICardEffectHandler, ShotgunHandler>();
        services.AddScoped<ICardEffectHandler, HealHandler>();
        services.AddScoped<ICardEffectHandler, ShieldHandler>();
        services.AddScoped<ICardEffectHandler, ZombieInfectionHandler>();
        services.AddScoped<ICardEffectHandler, PowerZombieInfectionHandler>();
        services.AddScoped<ICardEffectResolver, CardEffectResolver>();
        services.AddScoped<ICardPlayValidator, CardPlayValidator>();

        services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();
        services.AddScoped<ICardDealingService, CardDealingService>();
        services.AddScoped<IVotingService, VotingService>();
        services.AddScoped<IWinConditionService, WinConditionService>();
        services.AddScoped<IMatchCompletionService, MatchCompletionService>();
        services.AddScoped<IGameRulesEngine, GameRulesEngine>();
        services.AddSingleton<IMatchSimulationService, MatchSimulationService>();

        services.AddScoped<IGameSessionRecoveryService, GameSessionRecoveryService>();
        services.AddScoped<IGameBotExecutor, GameBotExecutor>();
        services.AddScoped<IGameLoopProcessor, GameLoopProcessor>();
        services.AddSingleton<IGameRealtimeNotifier, NullGameRealtimeNotifier>();

        return services;
    }
}
