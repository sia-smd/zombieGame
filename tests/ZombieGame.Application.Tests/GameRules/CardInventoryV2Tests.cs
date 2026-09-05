namespace ZombieGame.Application.Tests.GameRules;

using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Application.Simulation;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public class CardInventoryV2Tests
{
    [Fact]
    public void InitializeHands_SetsRoleCardAndFourInventorySlots()
    {
        var settings = new GameSettings();
        var dealing = GameTestBuilder.CreateDealingService(settings: settings);
        var playerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((playerId, PlayerRole.Human, true));

        dealing.InitializeHands(state);

        var hand = state.PlayerHands.First(h => h.UserId == playerId);
        Assert.NotEqual(Guid.Empty, hand.RoleCardId);
        Assert.NotNull(hand.InventorySlot1);
        Assert.NotNull(hand.InventorySlot2);
        Assert.NotNull(hand.InventorySlot3);
        Assert.NotNull(hand.InventorySlot4);
        Assert.Equal(ActionCardCatalog.Visitor, hand.InventorySlot1);
        Assert.Equal(ActionCardCatalog.Pass, hand.InventorySlot2);
    }

    [Fact]
    public void InitializeHands_AssignsRoleActionCards()
    {
        var settings = new GameSettings();
        var dealing = GameTestBuilder.CreateDealingService(settings: settings);

        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var powerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true),
            (powerId, PlayerRole.PowerZombie, true));

        dealing.InitializeHands(state);

        var humanHand = state.PlayerHands.First(h => h.UserId == humanId);
        var zombieHand = state.PlayerHands.First(h => h.UserId == zombieId);
        var powerHand = state.PlayerHands.First(h => h.UserId == powerId);

        Assert.Contains(ActionCardCatalog.HumanAction, humanHand.GetInventoryCardIds());
        Assert.Contains(ActionCardCatalog.Pass, humanHand.GetInventoryCardIds());
        Assert.Contains(ActionCardCatalog.ZombiePoison, zombieHand.GetInventoryCardIds());
        Assert.Contains(ActionCardCatalog.Pass, zombieHand.GetInventoryCardIds());
        Assert.Contains(ActionCardCatalog.ZombiePoison, powerHand.GetInventoryCardIds());
        Assert.Contains(ActionCardCatalog.Pass, powerHand.GetInventoryCardIds());
        Assert.Equal(4, humanHand.InventoryCount());
    }

    [Fact]
    public void ReplenishInventory_FillsOnlyEmptySlots()
    {
        var settings = new GameSettings();
        var dealing = GameTestBuilder.CreateDealingService(settings: settings);
        var playerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((playerId, PlayerRole.Human, true));
        var hand = state.PlayerHands.First(h => h.UserId == playerId);
        hand.InventorySlot1 = TestCards.Shotgun.Id;
        hand.InventorySlot2 = null;

        dealing.ReplenishInventory(state);

        Assert.Equal(TestCards.Shotgun.Id, hand.InventorySlot1);
        Assert.NotNull(hand.InventorySlot2);
    }

    [Fact]
    public void ConsumeAfterPlay_RemovesShotgunButKeepsShield()
    {
        var consumption = GameTestBuilder.CreateConsumptionService();
        var hand = new PlayerCardState
        {
            UserId = Guid.NewGuid(),
            RoleCardId = TestCards.Human.Id,
            InventorySlot1 = TestCards.Shotgun.Id,
            InventorySlot2 = TestCards.Shield.Id
        };

        consumption.ConsumeAfterPlay(hand, TestCards.Shotgun);
        consumption.ConsumeAfterPlay(hand, TestCards.Shield);

        Assert.Null(hand.InventorySlot1);
        Assert.Equal(TestCards.Shield.Id, hand.InventorySlot2);
    }

    [Fact]
    public void ConsumeAfterPlay_KeepsVisitorAndPoisonEvenWithSlotIndex()
    {
        var consumption = GameTestBuilder.CreateConsumptionService();
        var humanHand = new PlayerCardState
        {
            UserId = Guid.NewGuid(),
            RoleCardId = TestCards.Human.Id,
            InventorySlot1 = ActionCardCatalog.Visitor,
            InventorySlot2 = ActionCardCatalog.Pass
        };
        var zombieHand = new PlayerCardState
        {
            UserId = Guid.NewGuid(),
            RoleCardId = RoleCardCatalog.Infection,
            InventorySlot1 = ActionCardCatalog.ZombiePoison,
            InventorySlot2 = ActionCardCatalog.Pass
        };

        consumption.ConsumeAfterPlay(humanHand, TestCards.HumanAction, inventorySlotIndex: 0);
        consumption.ConsumeAfterPlay(zombieHand, TestCards.ZombiePoison, inventorySlotIndex: 0);

        Assert.Equal(ActionCardCatalog.Visitor, humanHand.InventorySlot1);
        Assert.Equal(ActionCardCatalog.ZombiePoison, zombieHand.InventorySlot1);
    }

    [Fact]
    public void ConsumeAfterPlay_RemovesShotgunFromSpecificSlot()
    {
        var consumption = GameTestBuilder.CreateConsumptionService();
        var hand = new PlayerCardState
        {
            UserId = Guid.NewGuid(),
            RoleCardId = TestCards.Human.Id,
            InventorySlot1 = TestCards.Shotgun.Id,
            InventorySlot2 = TestCards.Shotgun.Id
        };

        consumption.ConsumeAfterPlay(hand, TestCards.Shotgun, inventorySlotIndex: 1);

        Assert.Equal(TestCards.Shotgun.Id, hand.InventorySlot1);
        Assert.Null(hand.InventorySlot2);
    }

    [Fact]
    public void PassDoesNotChangeInventory()
    {
        var settings = new GameSettings { MaxPassActionsPerDay = 0, ActionsPerTurn = 2 };
        var dealing = GameTestBuilder.CreateDealingService(settings: settings);
        var dayEvents = GameTestBuilder.CreateDayEventService();
        var engine = new GameRulesEngine(
            new RoleAssignmentService(),
            dealing,
            dayEvents,
            new CardEffectResolver(GameTestBuilder.CreateCardHandlers(dayEvents), dayEvents),
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new SimulationMatchCompletionService(),
            Options.Create(settings));

        var playerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((playerId, PlayerRole.Human, true));
        state.CurrentPhase = GamePhase.Day;
        foreach (var player in state.Players)
        {
            player.ActionsPerTurn = 2;
            player.ActionsUsedThisTurn = 0;
        }

        GameTestBuilder.AddCardsToHand(state, playerId, TestCards.Shotgun, TestCards.Heal);
        var hand = state.PlayerHands.First(h => h.UserId == playerId);

        engine.PassAction(state, playerId);

        Assert.Equal(TestCards.Shotgun.Id, hand.InventorySlot1);
        Assert.Equal(TestCards.Heal.Id, hand.InventorySlot2);
    }
}
