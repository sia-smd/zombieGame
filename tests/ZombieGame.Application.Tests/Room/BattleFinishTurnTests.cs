namespace ZombieGame.Application.Tests.Room;

using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room.Battle;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public class BattleFinishTurnTests
{
    [Fact]
    public async Task PlayCardAsync_DoesNotFinishTurnWhenBothActionsSpent()
    {
        var (room, battle, service) = CreateBattleScenario();
        var playerA = battle.PlayerA;
        var playerB = battle.PlayerB;

        await service.PlayCardAsync(room, battle.BattleId, playerA, TestCards.Shotgun.Id, playerB, 0);
        Assert.False(battle.PlayerAFinished);
        Assert.Equal(1, room.Session.GetPlayer(playerA)!.RemainingActions);

        await service.PlayCardAsync(room, battle.BattleId, playerA, TestCards.Shotgun.Id, playerB, 1);
        Assert.False(battle.PlayerAFinished);
        Assert.Equal(0, room.Session.GetPlayer(playerA)!.RemainingActions);
        Assert.Equal(2, battle.CardsPlayed.Count(c => c.PlayerId == playerA));
    }

    [Fact]
    public async Task FinishTurnAsync_MarksPlayerFinishedAfterCardsQueued()
    {
        var (room, battle, service) = CreateBattleScenario();
        var playerA = battle.PlayerA;
        var playerB = battle.PlayerB;

        await service.PlayCardAsync(room, battle.BattleId, playerA, TestCards.Shotgun.Id, playerB, 0);
        await service.PlayCardAsync(room, battle.BattleId, playerA, TestCards.Shotgun.Id, playerB, 1);
        await service.FinishTurnAsync(room, battle.BattleId, playerA);

        Assert.True(battle.PlayerAFinished);
        Assert.False(battle.PlayerBFinished);
        Assert.Equal(BattleAggregateStatus.InProgress, battle.Status);
    }

    [Fact]
    public async Task FinishTurnAsync_KeepsPermanentCardsAfterResolution()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var battleId = Guid.NewGuid();

        var session = GameTestBuilder.CreateSession(
            (playerA, PlayerRole.Human, true),
            (playerB, PlayerRole.Zombie, true));
        GameTestBuilder.ResetActionPoints(session);
        GameTestBuilder.AddCardsToHand(session, playerA, TestCards.HumanAction, TestCards.Pass);
        GameTestBuilder.AddCardsToHand(session, playerB, TestCards.ZombiePoison, TestCards.Pass);

        var room = new RoomState
        {
            MatchId = matchId,
            CurrentPhase = RoomPhase.CardBattle,
            DayNumber = 1,
            Session = session,
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

        var battle = new Battle
        {
            BattleId = battleId,
            MatchId = matchId,
            DayNumber = 1,
            PlayerA = playerA,
            PlayerB = playerB,
            Status = BattleAggregateStatus.InProgress
        };

        var store = new InMemoryBattleStore(battle);
        var match = new Match { Id = matchId };
        var dayEvents = GameTestBuilder.CreateDayEventService();
        var handlers = GameTestBuilder.CreateCardHandlers(dayEvents);
        var rulesEngine = new GameRulesEngine(
            new RoleAssignmentService(),
            GameTestBuilder.CreateDealingService(),
            dayEvents,
            new CardEffectResolver(handlers, dayEvents),
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new MatchCompletionService(new FakeCoinService(), new FakeMatchRepository(match), new FakeUnitOfWork()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));

        var service = new BattleService(
            store,
            rulesEngine,
            new FakeCardRegistry(),
            new CardConsumptionService(),
            new CardPlayValidator(),
            Options.Create(new RoomSettings()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));

        // Human plays Visitor; zombie plays Pass — neither permanent card should be wiped.
        await service.PlayCardAsync(room, battle.BattleId, playerA, TestCards.HumanAction.Id, null, 0);
        await service.PlayCardAsync(room, battle.BattleId, playerB, TestCards.Pass.Id, null, 1);
        await service.FinishTurnAsync(room, battle.BattleId, playerA);
        await service.FinishTurnAsync(room, battle.BattleId, playerB);

        var humanHand = room.Session.PlayerHands.First(h => h.UserId == playerA);
        var zombieHand = room.Session.PlayerHands.First(h => h.UserId == playerB);

        Assert.Equal(ActionCardCatalog.Visitor, humanHand.InventorySlot1);
        Assert.Equal(ActionCardCatalog.ZombiePoison, zombieHand.InventorySlot1);
        Assert.Equal(ActionCardCatalog.Pass, zombieHand.InventorySlot2);
        Assert.True(battle.IsFinished);
    }

    [Fact]
    public async Task FinishTurnAsync_KeepsPoisonAfterSuccessfulInfection()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var battleId = Guid.NewGuid();

        var session = GameTestBuilder.CreateSession(
            (playerA, PlayerRole.Human, true),
            (playerB, PlayerRole.Zombie, true));
        GameTestBuilder.ResetActionPoints(session);
        GameTestBuilder.AddCardsToHand(session, playerA, TestCards.Pass);
        GameTestBuilder.AddCardsToHand(session, playerB, TestCards.ZombiePoison, TestCards.Pass);

        var room = new RoomState
        {
            MatchId = matchId,
            CurrentPhase = RoomPhase.CardBattle,
            DayNumber = 1,
            Session = session,
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

        var battle = new Battle
        {
            BattleId = battleId,
            MatchId = matchId,
            DayNumber = 1,
            PlayerA = playerA,
            PlayerB = playerB,
            Status = BattleAggregateStatus.InProgress
        };

        var store = new InMemoryBattleStore(battle);
        var match = new Match { Id = matchId };
        var dayEvents = GameTestBuilder.CreateDayEventService();
        var handlers = GameTestBuilder.CreateCardHandlers(dayEvents);
        var rulesEngine = new GameRulesEngine(
            new RoleAssignmentService(),
            GameTestBuilder.CreateDealingService(),
            dayEvents,
            new CardEffectResolver(handlers, dayEvents),
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new MatchCompletionService(new FakeCoinService(), new FakeMatchRepository(match), new FakeUnitOfWork()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));

        var service = new BattleService(
            store,
            rulesEngine,
            new FakeCardRegistry(),
            new CardConsumptionService(),
            new CardPlayValidator(),
            Options.Create(new RoomSettings()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));

        await service.PlayCardAsync(room, battle.BattleId, playerB, TestCards.ZombiePoison.Id, playerA, 0);
        await service.FinishTurnAsync(room, battle.BattleId, playerA);
        await service.FinishTurnAsync(room, battle.BattleId, playerB);

        var zombieHand = room.Session.PlayerHands.First(h => h.UserId == playerB);
        Assert.Equal(PlayerRole.Zombie, room.Session.GetPlayer(playerA)!.Role);
        Assert.Equal(ActionCardCatalog.ZombiePoison, zombieHand.InventorySlot1);
    }

    private static (RoomState Room, Battle Battle, BattleService Service) CreateBattleScenario()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var battleId = Guid.NewGuid();

        var session = GameTestBuilder.CreateSession(
            (playerA, PlayerRole.Human, true),
            (playerB, PlayerRole.Human, true));
        GameTestBuilder.ResetActionPoints(session);
        GameTestBuilder.AddCardsToHand(session, playerA, TestCards.Shotgun, TestCards.Shotgun);

        var room = new RoomState
        {
            MatchId = matchId,
            CurrentPhase = RoomPhase.CardBattle,
            DayNumber = 1,
            Session = session,
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

        var battle = new Battle
        {
            BattleId = battleId,
            MatchId = matchId,
            DayNumber = 1,
            PlayerA = playerA,
            PlayerB = playerB,
            Status = BattleAggregateStatus.InProgress
        };

        var store = new InMemoryBattleStore(battle);
        var match = new Match { Id = matchId };
        var dayEvents = GameTestBuilder.CreateDayEventService();
        var handlers = GameTestBuilder.CreateCardHandlers(dayEvents);
        var rulesEngine = new GameRulesEngine(
            new RoleAssignmentService(),
            GameTestBuilder.CreateDealingService(),
            dayEvents,
            new CardEffectResolver(handlers, dayEvents),
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new MatchCompletionService(new FakeCoinService(), new FakeMatchRepository(match), new FakeUnitOfWork()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));

        var service = new BattleService(
            store,
            rulesEngine,
            new FakeCardRegistry(),
            new CardConsumptionService(),
            new CardPlayValidator(),
            Options.Create(new RoomSettings()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));

        return (room, battle, service);
    }

    private sealed class InMemoryBattleStore(Battle battle) : IBattleStore
    {
        public Task<Battle?> GetAsync(Guid matchId, Guid battleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Battle?>(matchId == battle.MatchId && battleId == battle.BattleId ? battle : null);

        public Task SaveAsync(Battle saved, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Battle>> GetByMatchAndDayAsync(Guid matchId, int dayNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Battle>>([]);

        public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
