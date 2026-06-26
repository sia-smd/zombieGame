namespace ZombieGame.Application.Tests.Support;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public static class TestCards
{
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
            PlayerHands = players.Select(p => new PlayerCardState { UserId = p.Id }).ToList()
        };

        return state;
    }

    public static GamePlayerState Player(this GameSessionState state, Guid id) =>
        state.GetPlayer(id)!;

    public static IDayEventService CreateDayEventService() =>
        new DayEventService(new IDayEventModifier[]
        {
            new NormalDayModifier(),
            new SunnyDayModifier(),
            new StormDayModifier()
        });

    public static InfectionTransformationService CreateTransformationService() =>
        new(new FakeCardRegistry());

    public static ICardEffectHandler[] CreateCardHandlers(
        IDayEventService? dayEvents = null,
        InfectionTransformationService? transformation = null,
        bool shieldBlocksPowerZombieInfection = false)
    {
        dayEvents ??= CreateDayEventService();
        transformation ??= CreateTransformationService();
        return
        [
            new ShotgunHandler(dayEvents),
            new HealHandler(transformation),
            new ShieldHandler(),
            new ZombieInfectionHandler(transformation),
            new PowerZombieInfectionHandler(transformation, shieldBlocksPowerZombieInfection)
        ];
    }

    public static void AddCardsToHand(GameSessionState state, Guid playerId, params CardDefinition[] cards)
    {
        var hand = state.PlayerHands.First(h => h.UserId == playerId);
        hand.CardIds.AddRange(cards.Select(c => c.Id));
    }

    public static void ResetActionPoints(GameSessionState state)
    {
        foreach (var player in state.Players)
        {
            player.ActionsPerTurn = 2;
            player.ActionsUsedThisTurn = 0;
        }
    }
}
