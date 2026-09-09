namespace ZombieGame.Application.Tests.GameRules;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;

public class CardEffectHandlerTests
{
    private readonly IDayEventService _dayEvents = GameTestBuilder.CreateDayEventService();
    private readonly ShotgunHandler _shotgun;
    private readonly HealHandler _heal;
    private readonly ShieldHandler _shield;
    private readonly HumanRoleHandler _humanRole;
    private readonly ZombieInfectionHandler _zombieInfect;
    private readonly PowerZombieInfectionHandler _powerInfect;

    public CardEffectHandlerTests()
    {
        var transformation = GameTestBuilder.CreateTransformationService();
        _shotgun = new ShotgunHandler(_dayEvents);
        _heal = new HealHandler(transformation);
        _shield = new ShieldHandler();
        _humanRole = new HumanRoleHandler();
        _zombieInfect = new ZombieInfectionHandler(
            transformation,
            new FakeCardRegistry(),
            new CardConsumptionService(),
            _heal);
        _powerInfect = new PowerZombieInfectionHandler(
            transformation,
            _dayEvents,
            new FakeCardRegistry(),
            new CardConsumptionService(),
            _heal);
    }

    [Fact]
    public void HumanRole_HasNoCombatEffect()
    {
        var humanId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((humanId, PlayerRole.Human, true));

        var result = _humanRole.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = humanId,
            Card = TestCards.Human
        });

        Assert.True(result.Success);
        Assert.False(result.RoleChanged);
        Assert.Contains("no combat effect", result.Message);
    }

    [Fact]
    public void ZombieInfection_BlockedByShieldPriority_WhenHumanHoldsShieldAndHasActions()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Shield);
        state.Player(humanId).ActionsUsedThisTurn = 1;

        var result = _zombieInfect.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = zombieId,
            TargetUserId = humanId,
            Card = TestCards.Infection
        });

        Assert.True(result.Success);
        Assert.Equal(CardEffectTelemetryKind.ZombieInfectionBlockedByShield, result.TelemetryKind);
        Assert.Contains("defensive priority", result.Message);
        Assert.Equal(PlayerRole.Human, state.Player(humanId).Role);
    }

    [Fact]
    public void ZombieInfection_PreemptedByHealPriority_WhenHumanHoldsHealAndZombieRevealed()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Heal);
        state.Player(zombieId).HasRevealedThisDay = true;

        var result = _zombieInfect.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = zombieId,
            TargetUserId = humanId,
            Card = TestCards.Infection
        });

        Assert.True(result.Success);
        Assert.Contains("Heal priority", result.Message);
        Assert.Equal(PlayerRole.Human, state.Player(zombieId).Role);
        Assert.Equal(PlayerRole.Human, state.Player(humanId).Role);
    }

    [Fact]
    public void Resolver_FirstInfect_ConvertsWhenHumanHoldsHealButDidNotPlayIt()
    {
        // Holding Heal without queueing it must not cancel the first infection.
        // Same-battle Heal that WAS queued is covered by HealInfectionBattleTests.
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        foreach (var p in state.Players)
        {
            p.ActionsPerTurn = 2;
            p.ActionsUsedThisTurn = 0;
        }

        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Heal);
        var resolver = new CardEffectResolver(GameTestBuilder.CreateCardHandlers(_dayEvents), _dayEvents);

        var result = resolver.Play(new CardEffectContext
        {
            State = state,
            ActorUserId = zombieId,
            TargetUserId = humanId,
            Card = TestCards.ZombiePoison
        });

        Assert.True(result.Success);
        Assert.True(result.RoleChanged);
        Assert.Equal(PlayerRole.Zombie, state.Player(humanId).Role);
        Assert.Equal(PlayerRole.Zombie, state.Player(zombieId).Role);
        Assert.True(state.Player(zombieId).HasRevealedThisDay);
    }

    [Fact]
    public void ZombieInfection_ConvertsHumanToZombie()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));

        var result = _zombieInfect.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = zombieId,
            TargetUserId = humanId,
            Card = TestCards.Infection
        });

        Assert.True(result.Success);
        Assert.True(result.RoleChanged);
        Assert.Equal(CardEffectTelemetryKind.ZombieInfectionSucceeded, result.TelemetryKind);
        Assert.Equal(PlayerRole.Zombie, state.Player(humanId).Role);
    }

    [Fact]
    public void ZombieInfection_BlockedByShield()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.Player(humanId).HasShield = true;

        var result = _zombieInfect.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = zombieId,
            TargetUserId = humanId,
            Card = TestCards.Infection
        });

        Assert.True(result.Success);
        Assert.Equal(CardEffectTelemetryKind.ZombieInfectionBlockedByShield, result.TelemetryKind);
        Assert.Equal(PlayerRole.Human, state.Player(humanId).Role);
        Assert.False(state.Player(humanId).HasShield);
    }

    [Fact]
    public void PowerZombieInfection_IgnoresShield_OnNormalDay()
    {
        var humanId = Guid.NewGuid();
        var powerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            DayEventType.NormalDay,
            (humanId, PlayerRole.Human, true),
            (powerId, PlayerRole.PowerZombie, true));
        state.Player(humanId).HasShield = true;

        var result = _powerInfect.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = powerId,
            TargetUserId = humanId,
            Card = TestCards.PowerInfection
        });

        Assert.True(result.Success);
        Assert.Equal(CardEffectTelemetryKind.PowerZombieInfectionSucceeded, result.TelemetryKind);
        Assert.Equal(PlayerRole.Zombie, state.Player(humanId).Role);
        Assert.False(state.Player(humanId).HasShield);
    }

    [Fact]
    public void PowerZombieInfection_BlockedByShield_OnStormDay()
    {
        var humanId = Guid.NewGuid();
        var powerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            DayEventType.Storm,
            (humanId, PlayerRole.Human, true),
            (powerId, PlayerRole.PowerZombie, true));
        state.Player(humanId).HasShield = true;

        var result = _powerInfect.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = powerId,
            TargetUserId = humanId,
            Card = TestCards.PowerInfection
        });

        Assert.True(result.Success);
        Assert.Equal(CardEffectTelemetryKind.PowerZombieInfectionBlockedByShield, result.TelemetryKind);
        Assert.Equal(PlayerRole.Human, state.Player(humanId).Role);
        Assert.False(state.Player(humanId).HasShield);
        Assert.True(state.Player(powerId).IsAlive);
    }

    [Fact]
    public void PowerZombieInfection_CannotPlay_OnSunnyDay()
    {
        var humanId = Guid.NewGuid();
        var powerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            DayEventType.SunnyDay,
            (humanId, PlayerRole.Human, true),
            (powerId, PlayerRole.PowerZombie, true));

        var context = new CardEffectContext
        {
            State = state,
            ActorUserId = powerId,
            TargetUserId = humanId,
            Card = TestCards.PowerInfection
        };

        Assert.False(_powerInfect.CanPlay(context));

        var resolver = new CardEffectResolver(GameTestBuilder.CreateCardHandlers(_dayEvents), _dayEvents);
        Assert.Throws<ZombieGame.Application.Common.ServiceException>(() => resolver.Play(new CardEffectContext
        {
            State = state,
            ActorUserId = powerId,
            TargetUserId = humanId,
            Card = TestCards.ZombiePoison
        }));
    }

    [Fact]
    public void Heal_NoEffectOnHiddenZombie()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.Player(zombieId).HasRevealedThisDay = false;

        var result = _heal.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = zombieId,
            Card = TestCards.Heal
        });

        Assert.True(result.Success);
        Assert.False(result.RoleChanged);
        Assert.Equal(PlayerRole.Zombie, state.Player(zombieId).Role);
    }

    [Fact]
    public void Heal_CuresZombieWithQueuedInfectionIntent()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.Player(zombieId).HasRevealedThisDay = false;
        state.Player(zombieId).HasInfectionIntentThisResolution = true;

        var result = _heal.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = zombieId,
            Card = TestCards.Heal
        });

        Assert.True(result.Success);
        Assert.True(result.RoleChanged);
        Assert.Equal(PlayerRole.Human, state.Player(zombieId).Role);
        Assert.True(state.Player(zombieId).InfectionPreemptedThisResolution);
    }
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.Player(zombieId).HasRevealedThisDay = true;

        var result = _heal.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = zombieId,
            Card = TestCards.Heal
        });

        Assert.True(result.Success);
        Assert.Equal(CardEffectTelemetryKind.ZombieCured, result.TelemetryKind);
        Assert.Equal(PlayerRole.Human, state.Player(zombieId).Role);
    }

    [Fact]
    public void Heal_DemotesRevealedPowerZombieToZombie()
    {
        var humanId = Guid.NewGuid();
        var powerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (powerId, PlayerRole.PowerZombie, true));
        state.Player(powerId).HasRevealedThisDay = true;

        var result = _heal.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = powerId,
            Card = TestCards.Heal
        });

        Assert.True(result.Success);
        Assert.True(result.RoleChanged);
        Assert.Equal(CardEffectTelemetryKind.PowerZombieDemoted, result.TelemetryKind);
        Assert.Equal(PlayerRole.Zombie, state.Player(powerId).Role);
    }

    [Fact]
    public void Heal_NoEffectOnHiddenPowerZombie()
    {
        var humanId = Guid.NewGuid();
        var powerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (powerId, PlayerRole.PowerZombie, true));
        state.Player(powerId).HasRevealedThisDay = false;

        var result = _heal.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = powerId,
            Card = TestCards.Heal
        });

        Assert.True(result.Success);
        Assert.False(result.RoleChanged);
        Assert.Equal(PlayerRole.PowerZombie, state.Player(powerId).Role);
    }

    [Fact]
    public void Shield_AbsorbsShotgun()
    {
        var human1 = Guid.NewGuid();
        var human2 = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (human1, PlayerRole.Human, true),
            (human2, PlayerRole.Human, true));
        state.Player(human2).HasShield = true;

        var result = _shotgun.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = human1,
            TargetUserId = human2,
            Card = TestCards.Shotgun
        });

        Assert.True(result.Success);
        Assert.True(state.Player(human2).IsAlive);
        Assert.False(state.Player(human2).HasShield);
    }

    [Fact]
    public void Shotgun_KillsZombieInOneHit_OnNormalDay()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            DayEventType.NormalDay,
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.Player(zombieId).HasRevealedThisDay = true;

        var result = _shotgun.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = zombieId,
            Card = TestCards.Shotgun
        });

        Assert.True(result.TargetKilled);
        Assert.False(state.Player(zombieId).IsAlive);
    }

    [Fact]
    public void Shotgun_KillsPowerZombieInTwoHits_OnNormalDay()
    {
        var humanId = Guid.NewGuid();
        var powerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            DayEventType.NormalDay,
            (humanId, PlayerRole.Human, true),
            (powerId, PlayerRole.PowerZombie, true));
        state.Player(powerId).HasRevealedThisDay = true;

        _shotgun.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = powerId,
            Card = TestCards.Shotgun
        });
        Assert.True(state.Player(powerId).IsAlive);

        var result = _shotgun.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = powerId,
            Card = TestCards.Shotgun
        });

        Assert.True(result.TargetKilled);
        Assert.False(state.Player(powerId).IsAlive);
    }

    [Fact]
    public void Shotgun_FriendlyFire_WhenHumanKillsHuman()
    {
        var human1 = Guid.NewGuid();
        var human2 = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (human1, PlayerRole.Human, true),
            (human2, PlayerRole.Human, true));

        var result = _shotgun.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = human1,
            TargetUserId = human2,
            Card = TestCards.Shotgun
        });

        Assert.True(result.FriendlyFire);
        Assert.True(result.TargetKilled);
    }

    [Fact]
    public void Shotgun_Storm_RequiresTwoHitsForZombie()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            DayEventType.Storm,
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.Player(zombieId).HasRevealedThisDay = true;

        _shotgun.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = zombieId,
            Card = TestCards.Shotgun
        });
        Assert.True(state.Player(zombieId).IsAlive);

        var result = _shotgun.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = zombieId,
            Card = TestCards.Shotgun
        });
        Assert.True(result.TargetKilled);
    }

    [Fact]
    public void Shotgun_Storm_CannotKillPowerZombie()
    {
        var humanId = Guid.NewGuid();
        var powerId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            DayEventType.Storm,
            (humanId, PlayerRole.Human, true),
            (powerId, PlayerRole.PowerZombie, true));
        state.Player(powerId).HasRevealedThisDay = true;

        var result = _shotgun.Apply(new CardEffectContext
        {
            State = state,
            ActorUserId = humanId,
            TargetUserId = powerId,
            Card = TestCards.Shotgun
        });

        Assert.False(result.TargetKilled);
        Assert.True(state.Player(powerId).IsAlive);
    }

    [Fact]
    public void ZombiePoison_CannotInfectAnotherZombie()
    {
        var attackerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (attackerId, PlayerRole.Zombie, true),
            (targetId, PlayerRole.Zombie, true));

        var context = new CardEffectContext
        {
            State = state,
            ActorUserId = attackerId,
            TargetUserId = targetId,
            Card = TestCards.ZombiePoison
        };

        Assert.False(_zombieInfect.CanPlay(context));

        var resolver = new CardEffectResolver(GameTestBuilder.CreateCardHandlers(_dayEvents), _dayEvents);
        var ex = Assert.Throws<Application.Common.ServiceException>(() => resolver.Play(context));
        Assert.Equal("Zombie Poison can only infect humans.", ex.Message);
        Assert.Equal(PlayerRole.Zombie, state.Player(targetId).Role);
        Assert.False(state.Player(attackerId).HasRevealedThisDay);
    }
}
