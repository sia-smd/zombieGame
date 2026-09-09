namespace ZombieGame.Application.Tests.Room.Concurrency;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.DTOs.Room;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Application.Room.Battle;
using ZombieGame.Application.Room.Phases;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;
using ZombieGame.Infrastructure.Game;

public static class ConcurrencyHarness
{
    public static BattleService CreateBattleService(IBattleStore store, GameSettings? gameSettings = null)
    {
        var settings = gameSettings ?? new GameSettings { ActionsPerTurn = 2 };
        var dayEvents = GameTestBuilder.CreateDayEventService();
        var handlers = GameTestBuilder.CreateCardHandlers(dayEvents);
        var match = new Match { Id = Guid.NewGuid() };
        var rules = new GameRulesEngine(
            new RoleAssignmentService(),
            GameTestBuilder.CreateDealingService(),
            dayEvents,
            new CardEffectResolver(handlers, dayEvents),
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new MatchCompletionService(new FakeCoinService(), new FakeMatchRepository(match), new FakeUnitOfWork()),
            Options.Create(settings));

        return new BattleService(
            store,
            rules,
            new FakeCardRegistry(),
            new CardConsumptionService(),
            new CardPlayValidator(),
            Options.Create(new RoomSettings()),
            Options.Create(settings));
    }

    public static (RoomState Room, Battle Battle) CreateInProgressBattle(
        Guid playerA,
        Guid playerB,
        PlayerRole roleA = PlayerRole.Human,
        PlayerRole roleB = PlayerRole.Human)
    {
        var matchId = Guid.NewGuid();
        var battleId = Guid.NewGuid();
        var session = GameTestBuilder.CreateSession(
            (playerA, roleA, true),
            (playerB, roleB, true));
        session.MatchId = matchId;
        GameTestBuilder.ResetActionPoints(session);

        var battle = new Battle
        {
            BattleId = battleId,
            MatchId = matchId,
            DayNumber = 1,
            PlayerA = playerA,
            PlayerB = playerB,
            Status = BattleAggregateStatus.InProgress,
            BattleEndsAt = DateTime.UtcNow.AddMinutes(1)
        };

        var room = new RoomState
        {
            MatchId = matchId,
            SessionToken = "test-token",
            CurrentPhase = RoomPhase.CardBattle,
            DayNumber = 1,
            Session = session,
            Players =
            [
                new RoomPlayerState { UserId = playerA, Username = "A", IsAlive = true, SeatIndex = 0 },
                new RoomPlayerState { UserId = playerB, Username = "B", IsAlive = true, SeatIndex = 1 }
            ],
            BattlePairs =
            [
                new BattlePair
                {
                    PairId = battleId,
                    Player1Id = playerA,
                    Player2Id = playerB,
                    Status = BattlePairStatus.InProgress,
                    BattleSession = new PairBattleSession { PairId = battleId }
                }
            ]
        };

        return (room, battle);
    }

    public static RoomStateMachine CreateMachine(
        IRoomStateStore store,
        IBattleStore battles,
        IRoomLock roomLock,
        IEnumerable<IRoomPhaseHandler> handlers,
        IRoomLifecycleCoordinator? lifecycle = null,
        IActiveMatchRegistry? active = null,
        IMatchRepository? matches = null,
        RoomSettings? roomSettings = null)
    {
        var matchId = Guid.Empty;
        return new RoomStateMachine(
            store,
            new NoOpHistory(),
            new NoOpChat(),
            roomLock,
            active ?? new InMemoryActiveMatchRegistry(),
            new NoOpUsers(),
            matches ?? new FakeMatchRepository(new Match { Id = matchId }),
            handlers,
            new NoOpEvents(),
            new NoOpPresenter(),
            lifecycle ?? new NoOpLifecycle(),
            battles,
            new NoOpSnapshots(),
            new NoOpEventsLog(),
            Options.Create(roomSettings ?? new RoomSettings { LockTtlSeconds = 30, LockWaitMilliseconds = 500 }),
            NullLogger<RoomStateMachine>.Instance);
    }

    public static RoomStateMachine CreateCardBattleMachine(
        IRoomStateStore store,
        IBattleStore battles,
        IRoomLock roomLock,
        RoomSettings? roomSettings = null)
    {
        var battleService = CreateBattleService(battles);
        var handlers = new IRoomPhaseHandler[]
        {
            new CardBattlePhaseHandler(battleService, new NoOpPresenter()),
            new BattleResultStayHandler(),
            new FinishedPhaseHandler()
        };
        return CreateMachine(store, battles, roomLock, handlers, roomSettings: roomSettings);
    }

    public static RoomStateMachine CreateStartGameMachine(
        IRoomStateStore store,
        IRoomLock roomLock,
        FakeCoinService coins,
        IActiveMatchRegistry active)
    {
        var handlers = new IRoomPhaseHandler[]
        {
            new DayStartPhaseHandler(
                new RoleAssignmentService(),
                GameTestBuilder.CreateDealingService(),
                GameTestBuilder.CreateDayEventService(),
                new WinConditionService(),
                coins,
                Options.Create(new GameSettings { ActionsPerTurn = 2 })),
            new FinishedPhaseHandler()
        };
        return CreateMachine(store, new CloningBattleStore(), roomLock, handlers, active: active);
    }
}

public sealed class NeverRoomLock : IRoomLock
{
    public Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        Task.FromResult<IAsyncDisposable?>(null);
}

public sealed class ExclusiveAlwaysLock : IRoomLock
{
    public Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        Task.FromResult<IAsyncDisposable?>(new Handle());

    private sealed class Handle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class NoOpHistory : IBattlePairHistoryStore
{
    public Task<IReadOnlySet<Guid>> GetPreviousOpponentsAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    public Task RecordPairAsync(Guid matchId, Guid player1Id, Guid player2Id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResetPlayerHistoryAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResetAllAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NoOpChat : IDiscussionChatStore
{
    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChatMessage>>([]);
    public Task AddMessageAsync(Guid matchId, ChatMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ClearAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NoOpEvents : IRoomEventPublisher
{
    public Task PublishAsync(RoomState room, IEnumerable<RoomEvent> events, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NoOpPresenter : IRoomStatePresenter
{
    public Task<RoomStateDto> BuildAsync(RoomState room, Guid viewerId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
    public Task<RoomStateDto> BuildPrivateAsync(RoomState room, Guid viewerId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
    public Task BroadcastAsync(RoomState room, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushPrivateToPairAsync(RoomState room, Guid pairId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NoOpLifecycle : IRoomLifecycleCoordinator
{
    public Task OnPhaseEnteredAsync(RoomState room, RoomPhase phase, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task OnPhaseCompletedAsync(RoomState room, RoomPhase completedPhase, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task OnMatchFinishedAsync(RoomState room, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SyncPlayerPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NoOpSnapshots : IRoomSnapshotStore
{
    public Task AppendAsync(RoomSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<RoomSnapshot?> GetLatestAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.FromResult<RoomSnapshot?>(null);
    public Task<IReadOnlyList<RoomSnapshot>> GetAllAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoomSnapshot>>([]);
    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NoOpEventsLog : IMatchEventLogStore
{
    public Task AppendAsync(MatchEventEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<MatchEventEntry>> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MatchEventEntry>>([]);
    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NoOpUsers : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
    public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
    public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Update(User user) { }
}

public sealed class CountingLifecycle : IRoomLifecycleCoordinator
{
    public int DayStartEnters;

    public Task OnPhaseEnteredAsync(RoomState room, RoomPhase phase, CancellationToken cancellationToken = default)
    {
        if (phase == RoomPhase.DayStart)
            Interlocked.Increment(ref DayStartEnters);
        return Task.CompletedTask;
    }

    public Task OnPhaseCompletedAsync(RoomState room, RoomPhase completedPhase, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task OnMatchFinishedAsync(RoomState room, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SyncPlayerPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class BattleResultStayHandler : IRoomPhaseHandler
{
    public RoomPhase Phase => RoomPhase.BattleResult;

    public Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay("results"));

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());

    public Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());
}

public sealed class TickAdvanceHandler(RoomPhase from, RoomPhase to) : IRoomPhaseHandler
{
    public int Ticks;
    public RoomPhase Phase => from;

    public Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Ticks);
        return Task.FromResult(RoomTransitionResult.Go(to, "advanced"));
    }

    public Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());
}
