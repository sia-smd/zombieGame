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

public class HealInfectionBattleTests
{
    [Fact]
    public async Task QueuedHeal_PreemptsSameBattleInfection()
    {
        var (room, battle, service, human, zombie) = CreateHumanVsZombie();
        GameTestBuilder.AddCardsToHand(room.Session, human, TestCards.Heal, TestCards.Pass);
        GameTestBuilder.AddCardsToHand(room.Session, zombie, TestCards.ZombiePoison, TestCards.Pass);

        await service.PlayCardAsync(room, battle.BattleId, human, TestCards.Heal.Id, zombie, 0);
        await service.PlayCardAsync(room, battle.BattleId, zombie, TestCards.ZombiePoison.Id, human, 0);
        await service.FinishTurnAsync(room, battle.BattleId, human);
        await service.FinishTurnAsync(room, battle.BattleId, zombie);

        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(human)!.Role);
        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(zombie)!.Role);
        Assert.Null(room.Session.PlayerHands.First(h => h.UserId == human).InventorySlot1);
        var zombieHand = room.Session.PlayerHands.First(h => h.UserId == zombie);
        Assert.DoesNotContain(ActionCardCatalog.ZombiePoison, zombieHand.GetInventoryCardIds());
        Assert.Contains(ActionCardCatalog.Visitor, zombieHand.GetInventoryCardIds());
        Assert.True(battle.CardsPlayed.Where(c => c.CardId == TestCards.ZombiePoison.Id).All(c => c.Skipped));
        Assert.False(room.Session.GetPlayer(human)!.HasInfectionIntentThisResolution);
        Assert.False(room.Session.GetPlayer(zombie)!.InfectionPreemptedThisResolution);
    }

    [Fact]
    public async Task HeldHealNotQueued_DoesNotPreemptInfection()
    {
        var (room, battle, service, human, zombie) = CreateHumanVsZombie();
        GameTestBuilder.AddCardsToHand(room.Session, human, TestCards.Heal, TestCards.Pass);
        GameTestBuilder.AddCardsToHand(room.Session, zombie, TestCards.ZombiePoison, TestCards.Pass);

        await service.PlayCardAsync(room, battle.BattleId, zombie, TestCards.ZombiePoison.Id, human, 0);
        await service.FinishTurnAsync(room, battle.BattleId, human);
        await service.FinishTurnAsync(room, battle.BattleId, zombie);

        Assert.Equal(PlayerRole.Zombie, room.Session.GetPlayer(human)!.Role);
        Assert.Equal(PlayerRole.Zombie, room.Session.GetPlayer(zombie)!.Role);
        Assert.Equal(TestCards.Heal.Id, room.Session.PlayerHands.First(h => h.UserId == human).InventorySlot1);
    }

    [Fact]
    public async Task Heal_DoesNotCureHiddenZombieWithoutQueuedAttack()
    {
        var (room, battle, service, human, zombie) = CreateHumanVsZombie();
        GameTestBuilder.AddCardsToHand(room.Session, human, TestCards.Heal, TestCards.Pass);
        GameTestBuilder.AddCardsToHand(room.Session, zombie, TestCards.Pass);

        await service.PlayCardAsync(room, battle.BattleId, human, TestCards.Heal.Id, zombie, 0);
        await service.PlayCardAsync(room, battle.BattleId, zombie, TestCards.Pass.Id, zombie, 0);
        await service.FinishTurnAsync(room, battle.BattleId, human);
        await service.FinishTurnAsync(room, battle.BattleId, zombie);

        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(human)!.Role);
        Assert.Equal(PlayerRole.Zombie, room.Session.GetPlayer(zombie)!.Role);
        Assert.False(room.Session.GetPlayer(zombie)!.HasRevealedThisDay);
    }

    [Fact]
    public async Task QueuedHeal_DemotesPowerZombie_AndSkipsInfection()
    {
        var (room, battle, service, human, power) = CreateHumanVsZombie(PlayerRole.PowerZombie);
        GameTestBuilder.AddCardsToHand(room.Session, human, TestCards.Heal, TestCards.Pass);
        GameTestBuilder.AddCardsToHand(room.Session, power, TestCards.ZombiePoison, TestCards.Pass);

        await service.PlayCardAsync(room, battle.BattleId, human, TestCards.Heal.Id, power, 0);
        await service.PlayCardAsync(room, battle.BattleId, power, TestCards.ZombiePoison.Id, human, 0);
        await service.FinishTurnAsync(room, battle.BattleId, human);
        await service.FinishTurnAsync(room, battle.BattleId, power);

        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(human)!.Role);
        Assert.Equal(PlayerRole.Zombie, room.Session.GetPlayer(power)!.Role);
        Assert.True(battle.CardsPlayed.Single(c => c.CardId == TestCards.ZombiePoison.Id).Skipped);
    }

    [Fact]
    public async Task ShieldThenHeal_PreemptsInfection_ShieldStaysOnHuman()
    {
        var (room, battle, service, human, zombie) = CreateHumanVsZombie();
        GameTestBuilder.AddCardsToHand(room.Session, human, TestCards.Shield, TestCards.Heal);
        GameTestBuilder.AddCardsToHand(room.Session, zombie, TestCards.ZombiePoison, TestCards.Pass);

        await service.PlayCardAsync(room, battle.BattleId, human, TestCards.Shield.Id, human, 0);
        await service.PlayCardAsync(room, battle.BattleId, human, TestCards.Heal.Id, zombie, 1);
        await service.PlayCardAsync(room, battle.BattleId, zombie, TestCards.ZombiePoison.Id, human, 0);
        await service.FinishTurnAsync(room, battle.BattleId, human);
        await service.FinishTurnAsync(room, battle.BattleId, zombie);

        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(human)!.Role);
        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(zombie)!.Role);
        Assert.True(room.Session.GetPlayer(human)!.HasShield);
        Assert.True(battle.CardsPlayed.Single(c => c.CardId == TestCards.ZombiePoison.Id).Skipped);
    }

    [Fact]
    public async Task ExtraQueuedInfectionAfterHeal_DoesNotApply()
    {
        var (room, battle, service, human, zombie) = CreateHumanVsZombie();
        GameTestBuilder.AddCardsToHand(room.Session, human, TestCards.Heal, TestCards.Pass);
        GameTestBuilder.AddCardsToHand(room.Session, zombie, TestCards.ZombiePoison, TestCards.Pass);

        await service.PlayCardAsync(room, battle.BattleId, human, TestCards.Heal.Id, zombie, 0);
        await service.PlayCardAsync(room, battle.BattleId, zombie, TestCards.ZombiePoison.Id, human, 0);
        battle.CardsPlayed.Add(new BattleCardPlay
        {
            PlayerId = zombie,
            CardId = TestCards.ZombiePoison.Id,
            InventorySlotIndex = 0,
            TargetUserId = human,
            PlayedAt = DateTime.UtcNow
        });
        await service.FinishTurnAsync(room, battle.BattleId, human);
        await service.FinishTurnAsync(room, battle.BattleId, zombie);

        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(human)!.Role);
        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(zombie)!.Role);
        Assert.Equal(2, battle.CardsPlayed.Count(c => c.CardId == TestCards.ZombiePoison.Id && c.Skipped));
    }

    [Fact]
    public async Task HealAndInfectionInCardsPlayed_InfectionDoesNotRunAfterHeal()
    {
        var (room, battle, service, human, zombie) = CreateHumanVsZombie();
        GameTestBuilder.AddCardsToHand(room.Session, human, TestCards.Heal, TestCards.Pass);
        GameTestBuilder.AddCardsToHand(room.Session, zombie, TestCards.ZombiePoison, TestCards.Pass);

        await service.PlayCardAsync(room, battle.BattleId, zombie, TestCards.ZombiePoison.Id, human, 0);
        await service.PlayCardAsync(room, battle.BattleId, human, TestCards.Heal.Id, zombie, 0);
        Assert.Contains(battle.CardsPlayed, c => c.CardId == TestCards.Heal.Id);
        Assert.Contains(battle.CardsPlayed, c => c.CardId == TestCards.ZombiePoison.Id);

        await service.FinishTurnAsync(room, battle.BattleId, human);
        await service.FinishTurnAsync(room, battle.BattleId, zombie);

        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(human)!.Role);
        Assert.Equal(PlayerRole.Human, room.Session.GetPlayer(zombie)!.Role);
        Assert.True(battle.IsFinished);
        Assert.Contains(battle.CardsPlayed, c => c.CardId == TestCards.Heal.Id && !c.Skipped);
        Assert.True(battle.CardsPlayed.Where(c => c.CardId == TestCards.ZombiePoison.Id).All(c => c.Skipped));
    }

    private static (RoomState Room, Battle Battle, BattleService Service, Guid Human, Guid Zombie) CreateHumanVsZombie(
        PlayerRole infectedRole = PlayerRole.Zombie)
    {
        var human = Guid.NewGuid();
        var zombie = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var battleId = Guid.NewGuid();

        var session = GameTestBuilder.CreateSession(
            (human, PlayerRole.Human, true),
            (zombie, infectedRole, true));
        GameTestBuilder.ResetActionPoints(session);

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
                    Player1Id = human,
                    Player2Id = zombie,
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
            PlayerA = human,
            PlayerB = zombie,
            Status = BattleAggregateStatus.InProgress
        };

        var store = new SingleBattleStore(battle);
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

        return (room, battle, service, human, zombie);
    }

    private sealed class SingleBattleStore(Battle battle) : IBattleStore
    {
        public Task<Battle?> GetAsync(Guid matchId, Guid battleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Battle?>(matchId == battle.MatchId && battleId == battle.BattleId ? battle : null);

        public Task SaveAsync(Battle saved, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Battle>> GetByMatchAndDayAsync(Guid matchId, int dayNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Battle>>([]);

        public Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
