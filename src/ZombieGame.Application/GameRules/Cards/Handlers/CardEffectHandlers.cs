namespace ZombieGame.Application.GameRules.Cards.Handlers;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public abstract class CardEffectHandlerBase : ICardEffectHandler
{
    public abstract string EffectKey { get; }

    public abstract bool CanPlay(CardEffectContext context);

    public abstract CardEffectResult Apply(CardEffectContext context);

    protected static void KillPlayer(GamePlayerState player)
    {
        player.IsAlive = false;
    }

    protected static void ResetCombatState(GamePlayerState player)
    {
        player.ShotgunHitCount = 0;
    }
}

public sealed class HumanRoleHandler : CardEffectHandlerBase
{
    public override string EffectKey => "human_role";

    public override bool CanPlay(CardEffectContext context) =>
        GameCombatRules.CanPlayHumanPresenceAction(context.Actor.Role) &&
        context.Actor.IsAlive &&
        context.TargetUserId == context.ActorUserId;

    public override CardEffectResult Apply(CardEffectContext context) =>
        CardEffectResult.Ok("Visitor action — no combat effect.");
}

public sealed class ShotgunHandler : CardEffectHandlerBase
{
    private readonly IDayEventService _dayEvents;

    public ShotgunHandler(IDayEventService dayEvents) => _dayEvents = dayEvents;

    public override string EffectKey => "shoot";

    public override bool CanPlay(CardEffectContext context) =>
        context.Actor.Role == PlayerRole.Human &&
        context.Actor.IsAlive &&
        context.Target.IsAlive;

    public override CardEffectResult Apply(CardEffectContext context)
    {
        var target = context.Target;
        var actor = context.Actor;
        var dayEvent = context.State.CurrentDayEvent;

        if (target.HasShield)
        {
            target.HasShield = false;
            return CardEffectResult.Ok("Shield absorbed the shotgun blast.");
        }

        if (target.Role == PlayerRole.Human)
        {
            KillPlayer(target);
            var friendlyFire = actor.Role == PlayerRole.Human;
            return new CardEffectResult
            {
                Success = true,
                Message = "Human was killed by shotgun.",
                FriendlyFire = friendlyFire,
                TargetKilled = true
            };
        }

        if (target.Role == PlayerRole.PowerZombie)
        {
            if (!_dayEvents.CanPowerZombieBeKilled(dayEvent))
                return CardEffectResult.Ok("PowerZombie cannot be killed during Storm. Only delay or defend.");

            target.RemainingHealth--;
            if (target.RemainingHealth <= 0)
            {
                KillPlayer(target);
                return new CardEffectResult
                {
                    Success = true,
                    Message = "PowerZombie eliminated.",
                    TargetKilled = true
                };
            }

            return CardEffectResult.Ok($"PowerZombie hit. {target.RemainingHealth} health remaining.");
        }

        if (target.Role == PlayerRole.Zombie)
        {
            var hitsRequired = _dayEvents.GetShotgunHitsRequired(PlayerRole.Zombie, dayEvent);
            target.ShotgunHitCount++;

            if (target.ShotgunHitCount >= hitsRequired)
            {
                KillPlayer(target);
                return new CardEffectResult
                {
                    Success = true,
                    Message = "Zombie eliminated.",
                    TargetKilled = true
                };
            }

            return CardEffectResult.Ok($"Zombie hit ({target.ShotgunHitCount}/{hitsRequired}).");
        }

        return CardEffectResult.Fail("Invalid shotgun target.");
    }
}

public sealed class HealHandler : CardEffectHandlerBase
{
    private readonly InfectionTransformationService _transformation;

    public HealHandler(InfectionTransformationService transformation) =>
        _transformation = transformation;

    public override string EffectKey => "heal";

    public override bool CanPlay(CardEffectContext context) =>
        context.Actor.Role == PlayerRole.Human &&
        context.Actor.IsAlive &&
        context.Target.IsAlive;

    public override CardEffectResult Apply(CardEffectContext context)
    {
        var target = context.Target;

        if (!GameCombatRules.CanHealTargetInCurrentResolution(target))
            return CardEffectResult.Ok("Heal had no effect on this target.");

        var newRole = GameCombatRules.GetHealResultRole(target.Role);
        if (newRole is null)
            return CardEffectResult.Fail("Invalid heal target.");

        var wasPowerZombie = target.Role == PlayerRole.PowerZombie;
        target.Role = newRole.Value;
        target.RemainingHealth = 1;
        target.InfectionPreemptedThisResolution = true;
        ResetCombatState(target);

        var hand = context.State.PlayerHands.FirstOrDefault(h => h.UserId == context.TargetUserId);
        if (hand is not null)
        {
            if (wasPowerZombie)
                _transformation.DemotePowerZombieToZombie(hand);
            else
                _transformation.RevertZombieToHuman(hand);
        }

        return new CardEffectResult
        {
            Success = true,
            Message = wasPowerZombie
                ? "PowerZombie demoted to regular Zombie."
                : "Zombie cured and converted to Human.",
            RoleChanged = true,
            TelemetryKind = wasPowerZombie
                ? CardEffectTelemetryKind.PowerZombieDemoted
                : CardEffectTelemetryKind.ZombieCured
        };
    }
}

public sealed class ShieldHandler : CardEffectHandlerBase
{
    public override string EffectKey => "shield";

    public override bool CanPlay(CardEffectContext context) =>
        context.Actor.Role is PlayerRole.Human or PlayerRole.Zombie &&
        context.Actor.IsAlive &&
        context.TargetUserId == context.ActorUserId;

    public override CardEffectResult Apply(CardEffectContext context)
    {
        if (context.Target.HasShield)
            return CardEffectResult.Ok("Shield already active.");

        context.Target.HasShield = true;
        return CardEffectResult.Ok("Shield activated.");
    }
}

public sealed class ZombieInfectionHandler : CardEffectHandlerBase
{
    private readonly InfectionTransformationService _transformation;
    private readonly ICardRegistry _cards;
    private readonly ICardConsumptionService _consumption;
    private readonly CombatPriorityService _priority;

    public ZombieInfectionHandler(
        InfectionTransformationService transformation,
        ICardRegistry cards,
        ICardConsumptionService consumption,
        HealHandler heal)
    {
        _transformation = transformation;
        _cards = cards;
        _consumption = consumption;
        _priority = new CombatPriorityService(cards, consumption, heal);
    }

    public override string EffectKey => "infect";

    public override bool CanPlay(CardEffectContext context) =>
        context.Actor.Role == PlayerRole.Zombie &&
        context.Actor.IsAlive &&
        context.Target.IsAlive &&
        context.Target.Role == PlayerRole.Human;

    public override CardEffectResult Apply(CardEffectContext context)
    {
        var target = context.Target;
        var targetHand = context.State.PlayerHands.FirstOrDefault(h => h.UserId == context.TargetUserId);

        if (GameCombatRules.InfectionBlockedByShieldPriority(target, targetHand, _cards.GetById))
        {
            // Auto-spend the pending shield (same priority model as heal-before-infect).
            var shieldCard = targetHand!.FindAvailableEffectCard(_cards.GetById, "shield");
            if (shieldCard is not null)
            {
                target.ActionsUsedThisTurn++;
                _consumption.ConsumeAfterPlay(targetHand, shieldCard);
            }

            return new CardEffectResult
            {
                Success = true,
                Message = "Infection blocked by defensive priority.",
                TelemetryKind = CardEffectTelemetryKind.ZombieInfectionBlockedByShield
            };
        }

        var healPreempt = _priority.TryHealBeforeInfection(context);
        if (healPreempt is not null)
            return healPreempt;

        if (target.HasShield)
        {
            target.HasShield = false;
            return new CardEffectResult
            {
                Success = true,
                Message = "Shield blocked infection.",
                TelemetryKind = CardEffectTelemetryKind.ZombieInfectionBlockedByShield
            };
        }

        target.Role = PlayerRole.Zombie;
        target.RemainingHealth = 1;
        ResetCombatState(target);
        var transform = _transformation.ApplyHumanToZombie(context.State, context.TargetUserId);
        return new CardEffectResult
        {
            Success = true,
            Message = "Human infected and converted to Zombie.",
            RoleChanged = true,
            TelemetryKind = CardEffectTelemetryKind.ZombieInfectionSucceeded,
            InfectionTransform = transform
        };
    }
}

public sealed class PowerZombieInfectionHandler : CardEffectHandlerBase
{
    private readonly IDayEventService _dayEvents;
    private readonly InfectionTransformationService _transformation;
    private readonly ICardRegistry _cards;
    private readonly ICardConsumptionService _consumption;
    private readonly CombatPriorityService _priority;

    public PowerZombieInfectionHandler(
        InfectionTransformationService transformation,
        IDayEventService dayEvents,
        ICardRegistry cards,
        ICardConsumptionService consumption,
        HealHandler heal)
    {
        _transformation = transformation;
        _dayEvents = dayEvents;
        _cards = cards;
        _consumption = consumption;
        _priority = new CombatPriorityService(cards, consumption, heal);
    }

    public override string EffectKey => "power_zombie";

    public override bool CanPlay(CardEffectContext context) =>
        context.Actor.Role == PlayerRole.PowerZombie &&
        GameCombatRules.CanPowerZombieAttack(context.State.CurrentDayEvent) &&
        context.Actor.IsAlive &&
        context.Target.IsAlive &&
        context.Target.Role == PlayerRole.Human;

    public override CardEffectResult Apply(CardEffectContext context)
    {
        var target = context.Target;
        var dayEvent = context.State.CurrentDayEvent;
        var targetHand = context.State.PlayerHands.FirstOrDefault(h => h.UserId == context.TargetUserId);

        if (GameCombatRules.InfectionBlockedByShieldPriority(target, targetHand, _cards.GetById))
        {
            // Auto-spend the pending shield (same priority model as heal-before-infect).
            var shieldCard = targetHand!.FindAvailableEffectCard(_cards.GetById, "shield");
            if (shieldCard is not null)
            {
                target.ActionsUsedThisTurn++;
                _consumption.ConsumeAfterPlay(targetHand, shieldCard);
            }

            return new CardEffectResult
            {
                Success = true,
                Message = "Infection blocked by defensive priority.",
                TelemetryKind = CardEffectTelemetryKind.ZombieInfectionBlockedByShield
            };
        }

        var healPreempt = _priority.TryHealBeforeInfection(context);
        if (healPreempt is not null)
            return healPreempt;

        if (_dayEvents.DoesShieldBlockPowerZombieInfection(dayEvent) && target.HasShield)
        {
            target.HasShield = false;
            return new CardEffectResult
            {
                Success = true,
                Message = "Shield blocked PowerZombie infection.",
                TelemetryKind = CardEffectTelemetryKind.PowerZombieInfectionBlockedByShield
            };
        }

        target.Role = PlayerRole.Zombie;
        target.RemainingHealth = 1;
        target.HasShield = false;
        ResetCombatState(target);
        var transform = _transformation.ApplyHumanToZombie(context.State, context.TargetUserId);

        return new CardEffectResult
        {
            Success = true,
            Message = "Human infected by PowerZombie.",
            RoleChanged = true,
            TelemetryKind = CardEffectTelemetryKind.PowerZombieInfectionSucceeded,
            InfectionTransform = transform
        };
    }
}
