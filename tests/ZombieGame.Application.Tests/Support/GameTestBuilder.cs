namespace ZombieGame.Application.Tests.Support;

using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public static class TestCards
{
    public static readonly CardDefinition Human = new()
    {
        Id = RoleCardCatalog.Human,
        Name = "Human",
        Type = CardType.Human,
        EffectKey = "human_role"
    };

    public static readonly CardDefinition Shotgun = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
        Name = "Shotgun",
        Type = CardType.Shotgun,
        EffectKey = "shoot"
    };

    public static readonly CardDefinition Heal = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
        Name = "Medkit",
        Type = CardType.Heal,
        EffectKey = "heal"
    };

    public static readonly CardDefinition Shield = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
        Name = "Barricade",
        Type = CardType.Shield,
        EffectKey = "shield"
    };

    public static readonly CardDefinition Infection = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
        Name = "Infection",
        Type = CardType.Zombie,
        EffectKey = "infect"
    };

    public static readonly CardDefinition PowerInfection = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111106"),
        Name = "Alpha Zombie",
        Type = CardType.PowerZombie,
        EffectKey = "power_zombie"
    };

    public static readonly CardDefinition HumanAction = new()
    {
        Id = ActionCardCatalog.Visitor,
        Name = "Visitor",
        Type = CardType.Human,
        EffectKey = "human_role"
    };

    public static readonly CardDefinition ZombiePoison = new()
    {
        Id = ActionCardCatalog.ZombiePoison,
        Name = "Zombie Poison",
        Type = CardType.Zombie,
        EffectKey = "zombie_poison"
    };

    public static readonly CardDefinition Pass = new()
    {
        Id = ActionCardCatalog.Pass,
        Name = "Pass",
        Type = CardType.EventCard,
        EffectKey = "pass"
    };
}

public static class GameTestBuilder
{
    public static GameSessionState CreateSession(
        params (Guid Id, PlayerRole Role, bool Alive)[] players) =>
        CreateSession(DayEventType.NormalDay, players);

    public static GameSessionState CreateSession(
        DayEventType dayEvent,
        params (Guid Id, PlayerRole Role, bool Alive)[] players)
    {
        var state = new GameSessionState
        {
            MatchId = Guid.NewGuid(),
            SessionToken = "test-token",
            CurrentPhase = GamePhase.Day,
            TurnNumber = 1,
            CurrentDayEvent = dayEvent,
            Players = players.Select((p, i) => new GamePlayerState
            {
                UserId = p.Id,
                Username = $"Player{i}",
                Role = p.Role,
                IsAlive = p.Alive,
                SeatIndex = i,
                RemainingHealth = p.Role == PlayerRole.PowerZombie ? 2 : 1
            }).ToList(),
            PlayerHands = players.Select(p => new PlayerCardState
            {
                UserId = p.Id,
                RoleCardId = RoleCardCatalog.GetRoleCardId(p.Role)
            }).ToList()
        };

        return state;
    }

    public static GamePlayerState Player(this GameSessionState state, Guid id) =>
        state.GetPlayer(id)!;

    public static IDayEventService CreateDayEventService() =>
        GameRulesComposition.CreateDayEventService();

    public static InfectionTransformationService CreateTransformationService() =>
        GameRulesComposition.CreateTransformationService(new FakeCardRegistry());

    public static ICardEffectHandler[] CreateCardHandlers(
        IDayEventService? dayEvents = null,
        InfectionTransformationService? transformation = null)
    {
        dayEvents ??= CreateDayEventService();
        transformation ??= CreateTransformationService();
        var registry = new FakeCardRegistry();
        var consumption = new CardConsumptionService();
        return GameRulesComposition.CreateCardHandlers(dayEvents, transformation, registry, consumption);
    }

    public static void AddCardsToHand(GameSessionState state, Guid playerId, params CardDefinition[] cards)
    {
        var hand = state.PlayerHands.First(h => h.UserId == playerId);
        var player = state.GetPlayer(playerId)!;
        if (hand.RoleCardId == Guid.Empty)
            hand.RoleCardId = RoleCardCatalog.GetRoleCardId(player.Role);

        foreach (var card in cards)
        {
            if (RoleCardCatalog.IsRoleCard(card.Id))
            {
                hand.RoleCardId = card.Id;
                continue;
            }

            if (hand.InventorySlot1 is null)
                hand.InventorySlot1 = card.Id;
            else if (hand.InventorySlot2 is null)
                hand.InventorySlot2 = card.Id;
            else if (hand.InventorySlot3 is null)
                hand.InventorySlot3 = card.Id;
            else if (hand.InventorySlot4 is null)
                hand.InventorySlot4 = card.Id;
        }
    }

    public static CardDealingService CreateDealingService(ICardRegistry? registry = null, GameSettings? settings = null)
    {
        registry ??= new FakeCardRegistry();
        settings ??= new GameSettings();
        var generator = new InventoryCardGenerator(registry, Options.Create(settings));
        return new CardDealingService(generator, Options.Create(settings));
    }

    public static CardConsumptionService CreateConsumptionService() => new();

    public static void ResetActionPoints(GameSessionState state)
    {
        foreach (var player in state.Players)
        {
            player.ActionsPerTurn = 2;
            player.ActionsUsedThisTurn = 0;
        }
    }
}
