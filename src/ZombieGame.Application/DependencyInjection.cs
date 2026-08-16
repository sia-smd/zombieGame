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
using ZombieGame.Application.Room;
using ZombieGame.Application.Room.Battle;
using ZombieGame.Application.Room.Bots;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RoomSettings>(configuration.GetSection(RoomSettings.SectionName));
        services.Configure<GameSettings>(configuration.GetSection(GameSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<AccountSettings>(configuration.GetSection(AccountSettings.SectionName));
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));
        services.Configure<SmsSettings>(configuration.GetSection(SmsSettings.SectionName));
        services.Configure<RateLimitSettings>(configuration.GetSection(RateLimitSettings.SectionName));
        services.Configure<ClientSettings>(configuration.GetSection(ClientSettings.SectionName));
        services.Configure<DayEventOptions>(configuration.GetSection(DayEventOptions.SectionName));

        services.AddSingleton<IMatchmakingQueue, MatchmakingQueue>();
        services.AddSingleton<IMatchmakingPendingMatchStore, MatchmakingPendingMatchStore>();
        services.AddSingleton<IUserPresenceTracker, UserPresenceTracker>();
        services.AddSingleton<IRoomInviteStore, RoomInviteStore>();
        services.AddScoped<IMatchmakingService, MatchmakingService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IPlayerProfileService, PlayerProfileService>();
        services.AddScoped<IPlayerProgressService, PlayerProgressService>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddSingleton<IForbiddenWordsService, ForbiddenWordsService>();
        services.AddSingleton<IAvatarCatalogService, AvatarCatalogService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IActiveMatchQueryService, ActiveMatchQueryService>();
        services.AddScoped<ICoinService, CoinService>();
        services.AddScoped<IBotService, BotService>();

        GameRulesComposition.RegisterDayEvents(services);

        services.AddScoped<InfectionTransformationService>();

        services.AddScoped<HealHandler>();
        services.AddScoped<ICardEffectHandler, HumanRoleHandler>();
        services.AddScoped<ICardEffectHandler, ShotgunHandler>();
        services.AddScoped<ICardEffectHandler>(sp => sp.GetRequiredService<HealHandler>());
        services.AddScoped<ICardEffectHandler, ShieldHandler>();
        services.AddScoped<ICardEffectHandler, ZombieInfectionHandler>();
        services.AddScoped<ICardEffectHandler, PowerZombieInfectionHandler>();
        services.AddScoped<ICardEffectResolver, CardEffectResolver>();
        services.AddScoped<ICardPlayValidator, CardPlayValidator>();

        services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();
        services.AddScoped<IInventoryCardGenerator, InventoryCardGenerator>();
        services.AddScoped<ICardConsumptionService, CardConsumptionService>();
        services.AddScoped<ICardDealingService, CardDealingService>();
        services.AddScoped<IVotingService, VotingService>();
        services.AddScoped<IWinConditionService, WinConditionService>();
        services.AddScoped<IMatchCompletionService, MatchCompletionService>();
        services.AddScoped<IGameRulesEngine, GameRulesEngine>();
        services.AddSingleton<IMatchSimulationService, MatchSimulationService>();

        services.AddScoped<IGameSessionRecoveryService, GameSessionRecoveryService>();
        services.AddScoped<IGameBotExecutor, GameBotExecutor>();
        services.AddScoped<IGameLoopProcessor, GameLoopProcessor>();
        services.AddScoped<IRoomLoopProcessor, RoomLoopProcessor>();
        services.AddScoped<IRoomBotExecutor, RoomBotExecutor>();
        services.AddSingleton<IGameRealtimeNotifier, NullGameRealtimeNotifier>();
        services.AddSingleton<IRoomRealtimeNotifier, NullRoomRealtimeNotifier>();

        services.AddSingleton<OpponentSelectionService>();
        services.AddSingleton<InvitationCoordinator>();
        services.AddScoped<IBattleService, Room.Battle.BattleService>();
        services.AddScoped<IRoomCommandValidator, RoomCommandValidator>();
        services.AddScoped<IRoomSnapshotService, RoomSnapshotService>();
        services.AddScoped<IMatchEventLogService, MatchEventLogService>();
        services.AddScoped<IMatchSummaryService, MatchSummaryService>();
        services.AddScoped<IPlayerReconnectService, PlayerReconnectService>();
        services.AddScoped<IRoomLifecycleCoordinator, RoomLifecycleCoordinator>();
        services.AddScoped<IRoomInspectorService, RoomInspectorService>();
        services.AddScoped<IRoomStateMachine, RoomStateMachine>();
        services.AddScoped<IRoomEventPublisher, RoomEventPublisher>();
        services.AddScoped<IRoomStatePresenter, RoomStatePresenter>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.DayStartPhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.OpponentSelectionPhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.BattlePreparationPhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.CardBattlePhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.BattleResultPhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.DaySummaryPhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.DiscussionPhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.VotingPhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.VoteResultPhaseHandler>();
        services.AddScoped<IRoomPhaseHandler, Room.Phases.FinishedPhaseHandler>();

        return services;
    }
}
