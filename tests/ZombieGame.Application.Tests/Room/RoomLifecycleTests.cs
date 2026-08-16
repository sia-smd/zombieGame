namespace ZombieGame.Application.Tests.Room;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Room;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Application.Room.Phases;
using ZombieGame.Application.Services;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public class RoomLifecycleCoordinatorTests
{
    [Fact]
    public async Task OnMatchFinished_CompletesMatchOnceAndClearsActivePlayers()
    {
        var playerId = Guid.NewGuid();
        var match = CreateMatch(playerId, MatchStatus.InProgress);
        var room = CreateFinishedRoom(match.Id, playerId);
        var completion = new RecordingCompletionService();
        var snapshots = new RecordingSnapshotService();
        var events = new RecordingEventLogService();
        var summary = new RecordingSummaryService();
        var active = new RecordingActiveMatchStore();
        var coordinator = new RoomLifecycleCoordinator(
            snapshots,
            events,
            new NoOpReconnectService(),
            summary,
            active,
            new FakeMatchRepository(match),
            completion);

        await coordinator.OnMatchFinishedAsync(room);
        await coordinator.OnMatchFinishedAsync(room);

        Assert.Equal(1, completion.Calls);
        Assert.Equal(2, snapshots.Captures);
        Assert.Equal(2, summary.Calls);
        Assert.Contains(playerId, active.Removed);
        Assert.Equal(MatchEventType.GameFinished, events.Types.Last());
    }

    [Fact]
    public async Task OnMatchFinished_SkipsCompletionWhenMatchAlreadyFinished()
    {
        var playerId = Guid.NewGuid();
        var match = CreateMatch(playerId, MatchStatus.Finished);
        var completion = new RecordingCompletionService();
        var coordinator = new RoomLifecycleCoordinator(
            new RecordingSnapshotService(),
            new RecordingEventLogService(),
            new NoOpReconnectService(),
            new RecordingSummaryService(),
            new RecordingActiveMatchStore(),
            new FakeMatchRepository(match),
            completion);

        await coordinator.OnMatchFinishedAsync(CreateFinishedRoom(match.Id, playerId));

        Assert.Equal(0, completion.Calls);
    }

    private static Match CreateMatch(Guid playerId, MatchStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            Status = status,
            Players = [new MatchPlayer { UserId = playerId, Role = PlayerRole.Human, IsAlive = true }]
        };

    private static RoomState CreateFinishedRoom(Guid matchId, Guid playerId)
    {
        var room = new RoomState
        {
            MatchId = matchId,
            CurrentPhase = RoomPhase.Finished,
            WinTeam = WinTeam.Humans,
            Players = [new RoomPlayerState { UserId = playerId, IsAlive = true }]
        };
        room.Session.Players.Add(new GamePlayerState
        {
            UserId = playerId,
            Role = PlayerRole.Human,
            IsAlive = true
        });
        return room;
    }
}

public class RoomFinalizationTests
{
    [Fact]
    public async Task Tick_RetriesFinalizationUntilCleanupSucceeds()
    {
        var matchId = Guid.NewGuid();
        var room = new RoomState
        {
            MatchId = matchId,
            CurrentPhase = RoomPhase.Finished,
            WinTeam = WinTeam.Humans
        };
        var store = new InMemoryRoomStateStore(room);
        var lifecycle = new FlakyLifecycleCoordinator();
        var active = new InMemoryActiveMatchRegistry(matchId);
        var machine = CreateMachine(store, lifecycle, active);

        var first = await Record.ExceptionAsync(() => machine.TickAsync(matchId));
        Assert.NotNull(first);
        Assert.NotNull(await store.GetAsync(matchId));
        Assert.Contains(matchId, await active.GetActiveMatchIdsAsync());

        await machine.TickAsync(matchId);

        Assert.Null(await store.GetAsync(matchId));
        Assert.Empty(await active.GetActiveMatchIdsAsync());
        Assert.Equal(2, lifecycle.Calls);
        Assert.True(store.Removed);
    }

    [Fact]
    public async Task FinishedHandler_IsRegisteredForTerminalTransitions()
    {
        var matchId = Guid.NewGuid();
        var room = new RoomState
        {
            MatchId = matchId,
            CurrentPhase = RoomPhase.VoteResult,
            WinTeam = WinTeam.None
        };
        var store = new InMemoryRoomStateStore(room);
        var lifecycle = new RecordingLifecycleCoordinator();
        var active = new InMemoryActiveMatchRegistry(matchId);
        var machine = CreateMachine(store, lifecycle, active, new VoteResultToFinishedHandler());

        await machine.TickAsync(matchId);

        Assert.Equal(1, lifecycle.FinishedCalls);
        Assert.True(store.Removed);
        Assert.Empty(await active.GetActiveMatchIdsAsync());
    }

    private static RoomStateMachine CreateMachine(
        InMemoryRoomStateStore store,
        IRoomLifecycleCoordinator lifecycle,
        IActiveMatchRegistry active,
        IRoomPhaseHandler? extraHandler = null)
    {
        var handlers = new List<IRoomPhaseHandler> { new FinishedPhaseHandler() };
        if (extraHandler is not null)
            handlers.Add(extraHandler);

        return new RoomStateMachine(
            store,
            new NoOpHistoryStore(),
            new NoOpChatStore(),
            new AlwaysRoomLock(),
            active,
            new NoOpUserRepository(),
            new FakeMatchRepository(new Match { Id = store.MatchId }),
            handlers,
            new NoOpEventPublisher(),
            new NoOpPresenter(),
            lifecycle,
            new NoOpBattleStore(),
            new NoOpSnapshotStore(),
            new NoOpEventLogStore(),
            Options.Create(new RoomSettings()),
            NullLogger<RoomStateMachine>.Instance);
    }
}

public class RoomResumeAuthorizationTests
{
    [Fact]
    public async Task ResumeMatch_RejectsInvalidTokenOrNonMember()
    {
        var userId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            SessionToken = "room-token",
            Players = [new MatchPlayer { UserId = userId }]
        };
        var service = CreateRoomService(match);

        await Assert.ThrowsAsync<ServiceException>(() =>
            service.ResumeMatchAsync(userId, match.Id, "wrong-token"));
        await Assert.ThrowsAsync<ServiceException>(() =>
            service.ResumeMatchAsync(Guid.NewGuid(), match.Id, "room-token"));
    }

    private static RoomService CreateRoomService(Match match) =>
        new(
            new NoOpStateMachine(),
            new FakeMatchRepository(match),
            new NoOpHistoryStore(),
            new NoOpChatStore(),
            new OpponentSelectionService(),
            new RoomCommandValidator(new FakeMatchRepository(match), new InMemoryRoomStateStore(), new NoOpBattleStore()),
            new ThrowingReconnectService(),
            new RecordingLifecycleCoordinator(),
            new NoOpPresenter(),
            Options.Create(new GameSettings()),
            Options.Create(new RoomSettings()),
            new FakeUnitOfWork());
}

sealed class VoteResultToFinishedHandler : IRoomPhaseHandler
{
    public RoomPhase Phase => RoomPhase.VoteResult;

    public Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        context.Room.WinTeam = WinTeam.Humans;
        return Task.FromResult(RoomTransitionResult.Go(
            RoomPhase.Finished,
            "Game finished.",
            new GameFinishedEvent(WinTeam.Humans)));
    }

    public Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());
}

sealed class RecordingCompletionService : IMatchCompletionService
{
    public int Calls { get; private set; }

    public Task CompleteMatchAsync(Match match, GameSessionState state, WinTeam winningTeam, CancellationToken cancellationToken = default)
    {
        Calls++;
        match.Status = MatchStatus.Finished;
        return Task.CompletedTask;
    }
}

sealed class RecordingSnapshotService : IRoomSnapshotService
{
    public int Captures { get; private set; }

    public Task CaptureAsync(RoomState room, SnapshotKind kind, CancellationToken cancellationToken = default)
    {
        Captures++;
        return Task.CompletedTask;
    }
}

sealed class RecordingEventLogService : IMatchEventLogService
{
    public List<MatchEventType> Types { get; } = [];

    public Task LogAsync(Guid matchId, MatchEventType type, Guid? actorUserId = null, object? payload = null, CancellationToken cancellationToken = default)
    {
        Types.Add(type);
        return Task.CompletedTask;
    }
}

sealed class RecordingSummaryService : IMatchSummaryService
{
    public int Calls { get; private set; }

    public Task<MatchSummary> FinalizeAsync(RoomState room, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(new MatchSummary { MatchId = room.MatchId, WinningTeam = room.WinTeam });
    }
}

sealed class RecordingActiveMatchStore : IPlayerActiveMatchStore
{
    public List<Guid> Removed { get; } = [];

    public Task<PlayerActiveMatch?> GetAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PlayerActiveMatch?>(null);

    public Task SetAsync(PlayerActiveMatch entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        Removed.Add(playerId);
        return Task.CompletedTask;
    }

    public Task TouchAsync(Guid playerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class NoOpReconnectService : IPlayerReconnectService
{
    public Task<PlayerActiveMatch> TrackPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PlayerActiveMatch { PlayerId = userId, MatchId = room.MatchId });

    public Task<PlayerActiveMatch?> OnDisconnectedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PlayerActiveMatch?>(null);

    public Task<ActiveMatchResponse?> GetActiveMatchAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ActiveMatchResponse?>(null);

    public Task<ResumeMatchResponse?> ResumeAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default) =>
        Task.FromResult<ResumeMatchResponse?>(null);
}

sealed class ThrowingReconnectService : IPlayerReconnectService
{
    public Task<PlayerActiveMatch> TrackPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Should not resume after access fails.");

    public Task<PlayerActiveMatch?> OnDisconnectedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Should not resume after access fails.");

    public Task<ActiveMatchResponse?> GetActiveMatchAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Should not resume after access fails.");

    public Task<ResumeMatchResponse?> ResumeAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Should not resume after access fails.");
}

sealed class FlakyLifecycleCoordinator : IRoomLifecycleCoordinator
{
    public int Calls { get; private set; }

    public Task OnPhaseEnteredAsync(RoomState room, RoomPhase phase, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnPhaseCompletedAsync(RoomState room, RoomPhase completedPhase, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnMatchFinishedAsync(RoomState room, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Calls == 1)
            throw new InvalidOperationException("simulated completion failure");
        return Task.CompletedTask;
    }

    public Task SyncPlayerPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

sealed class RecordingLifecycleCoordinator : IRoomLifecycleCoordinator
{
    public int FinishedCalls { get; private set; }

    public Task OnPhaseEnteredAsync(RoomState room, RoomPhase phase, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnPhaseCompletedAsync(RoomState room, RoomPhase completedPhase, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnMatchFinishedAsync(RoomState room, CancellationToken cancellationToken = default)
    {
        FinishedCalls++;
        return Task.CompletedTask;
    }

    public Task SyncPlayerPresenceAsync(RoomState room, Guid userId, string sessionToken, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

sealed class InMemoryRoomStateStore : IRoomStateStore
{
    private RoomState? _room;

    public InMemoryRoomStateStore(RoomState? room = null) => _room = room;

    public Guid MatchId => _room?.MatchId ?? Guid.Empty;
    public bool Removed { get; private set; }

    public Task<RoomState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_room?.MatchId == matchId ? _room : null);

    public Task SetAsync(RoomState state, CancellationToken cancellationToken = default)
    {
        _room = state;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (_room?.MatchId == matchId)
        {
            _room = null;
            Removed = true;
        }
        return Task.CompletedTask;
    }

    public Task<bool> TryAddAsync(Guid matchId, RoomState state, CancellationToken cancellationToken = default)
    {
        if (_room is not null)
            return Task.FromResult(false);
        _room = state;
        return Task.FromResult(true);
    }
}

sealed class InMemoryActiveMatchRegistry(Guid matchId) : IActiveMatchRegistry
{
    private readonly HashSet<Guid> _ids = [matchId];

    public Task RegisterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ids.Add(id);
        return Task.CompletedTask;
    }

    public Task UnregisterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ids.Remove(id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetActiveMatchIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(_ids.ToList());
}

sealed class AlwaysRoomLock : IRoomLock
{
    public Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        Task.FromResult<IAsyncDisposable?>(new LockHandle());

    private sealed class LockHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

sealed class NoOpHistoryStore : IBattlePairHistoryStore
{
    public Task<IReadOnlySet<Guid>> GetPreviousOpponentsAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    public Task RecordPairAsync(Guid matchId, Guid player1Id, Guid player2Id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ResetPlayerHistoryAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ResetAllAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class NoOpChatStore : IDiscussionChatStore
{
    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChatMessage>>([]);

    public Task AddMessageAsync(Guid matchId, ChatMessage message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ClearAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class NoOpBattleStore : IBattleStore
{
    public Task<Battle?> GetAsync(Guid matchId, Guid battleId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Battle?>(null);

    public Task SaveAsync(Battle battle, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<Battle>> GetByMatchAndDayAsync(Guid matchId, int dayNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Battle>>([]);

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class NoOpSnapshotStore : IRoomSnapshotStore
{
    public Task AppendAsync(RoomSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<RoomSnapshot?> GetLatestAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<RoomSnapshot?>(null);

    public Task<IReadOnlyList<RoomSnapshot>> GetAllAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoomSnapshot>>([]);

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class NoOpEventLogStore : IMatchEventLogStore
{
    public Task AppendAsync(MatchEventEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<MatchEventEntry>> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MatchEventEntry>>([]);

    public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class NoOpEventPublisher : IRoomEventPublisher
{
    public Task PublishAsync(RoomState room, IEnumerable<RoomEvent> events, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

sealed class NoOpPresenter : IRoomStatePresenter
{
    public Task<RoomStateDto> BuildAsync(RoomState room, Guid viewerId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<RoomStateDto> BuildPrivateAsync(RoomState room, Guid viewerId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task BroadcastAsync(RoomState room, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PushPrivateToPairAsync(RoomState room, Guid pairId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

sealed class NoOpUserRepository : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(null);

    public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(null);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(null);

    public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(null);

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Update(User user) { }
}

sealed class NoOpStateMachine : IRoomStateMachine
{
    public Task<RoomState?> GetStateAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<RoomState?>(null);

    public Task<RoomTransitionResult> DispatchAsync(Guid matchId, IRoomCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());

    public Task<RoomTransitionResult> TickAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());

    public Task<RoomState> InitializeRoomAsync(Match match, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RoomState { MatchId = match.Id });

        public Task SyncPlayersFromMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemovePlayerFromLobbyAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DiscardLobbyRoomAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

    public Task<RoomTransitionResult> StartGameAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());

    public Task<RoomState?> SetPresenceAsync(Guid matchId, Guid userId, bool connected, CancellationToken cancellationToken = default) =>
        Task.FromResult<RoomState?>(null);
}
