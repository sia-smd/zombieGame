namespace ZombieGame.Application.GameRules;

using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

/// <summary>
/// Applies heal-before-infect priority when infection is resolved.
/// </summary>
public sealed class CombatPriorityService
{
    private readonly ICardRegistry _cards;
    private readonly ICardConsumptionService _consumption;
    private readonly HealHandler _heal;

    public CombatPriorityService(
        ICardRegistry cards,
        ICardConsumptionService consumption,
        HealHandler heal)
    {
        _cards = cards;
        _consumption = consumption;
        _heal = heal;
    }

    public CardEffectResult? TryHealBeforeInfection(CardEffectContext infectContext)
    {
        var attacker = infectContext.Actor;
        if (!GameCombatRules.CanHealTarget(attacker))
            return null;

        foreach (var healer in infectContext.State.Players
                     .Where(p => p.IsAlive && p.Role == PlayerRole.Human && p.RemainingActions > 0)
                     .OrderBy(p => p.SeatIndex))
        {
            var hand = infectContext.State.PlayerHands.FirstOrDefault(h => h.UserId == healer.UserId);
            if (hand is null)
                continue;

            var healCard = hand.FindAvailableEffectCard(_cards.GetById, "heal");
            if (healCard is null)
                continue;

            healer.ActionsUsedThisTurn++;
            _consumption.ConsumeAfterPlay(hand, healCard);

            var healContext = new CardEffectContext
            {
                State = infectContext.State,
                ActorUserId = healer.UserId,
                TargetUserId = attacker.UserId,
                Card = healCard
            };

            var healResult = _heal.Apply(healContext);
            if (!healResult.RoleChanged)
                continue;

            return new CardEffectResult
            {
                Success = true,
                Message = "Heal priority cured attacker before infection.",
                RoleChanged = true,
                TelemetryKind = healResult.TelemetryKind
            };
        }

        return null;
    }
}
