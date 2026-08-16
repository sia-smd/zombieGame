namespace ZombieGame.Application.GameRules.Cards;

using ZombieGame.Application.Common;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public sealed class CardEffectResolver : ICardEffectResolver
{
    private readonly IReadOnlyDictionary<string, ICardEffectHandler> _handlers;
    private readonly IDayEventService _dayEvents;

    public CardEffectResolver(IEnumerable<ICardEffectHandler> handlers, IDayEventService dayEvents)
    {
        _handlers = handlers.ToDictionary(h => h.EffectKey, StringComparer.OrdinalIgnoreCase);
        _dayEvents = dayEvents;
    }

    public ICardEffectHandler Resolve(string effectKey) =>
        _handlers.TryGetValue(effectKey, out var handler)
            ? handler
            : throw new ServiceException($"No handler for effect '{effectKey}'.");

    public CardEffectResult Play(CardEffectContext context)
    {
        var effectKey = ResolvePlayEffectKey(context);

        if (context.Actor.Role == PlayerRole.PowerZombie &&
            effectKey is "infect" or "power_zombie" &&
            !GameCombatRules.CanPowerZombieAttack(context.State.CurrentDayEvent))
        {
            throw new ServiceException("PowerZombie cannot perform actions during SunnyDay.");
        }

        var handler = Resolve(effectKey);

        // Heal is always "playable" for humans; useless targets just do nothing instead of
        // hard-failing the hub call (which previously looked like a random client error).
        if (!handler.CanPlay(context))
        {
            if (context.Card.EffectKey.Equals("heal", StringComparison.OrdinalIgnoreCase))
                return CardEffectResult.Ok("Heal had no effect on this target.");

            throw new ServiceException(DescribeCannotPlay(context, effectKey));
        }

        // Resolve first, then reveal. Revealing before Apply made heal-before-infect fire on the
        // same attack (CanHealTarget saw HasRevealedThisDay from this play) and cancelled infection.
        var result = handler.Apply(context);
        RevealActorIfAttackCard(context, effectKey);
        return result;
    }

    private static string ResolvePlayEffectKey(CardEffectContext context)
    {
        if (context.Card.EffectKey.Equals("zombie_poison", StringComparison.OrdinalIgnoreCase))
            return context.Actor.Role == PlayerRole.PowerZombie ? "power_zombie" : "infect";

        return context.Card.EffectKey;
    }

    private static void RevealActorIfAttackCard(CardEffectContext context, string resolvedEffectKey)
    {
        if (resolvedEffectKey is "infect" or "power_zombie")
            context.Actor.HasRevealedThisDay = true;
    }

    private static string DescribeCannotPlay(CardEffectContext context, string resolvedEffectKey)
    {
        if (resolvedEffectKey is "infect" or "power_zombie")
        {
            if (!context.Target.IsAlive)
                return "Cannot infect a dead player.";
            if (context.Target.Role != PlayerRole.Human)
                return "Zombie Poison can only infect humans.";
        }

        return $"Cannot play {context.Card.Name} in the current situation.";
    }
}

public interface ICardPlayValidator
{
    void ValidateCardPlayable(Guid cardId);
    void ValidateRoleCanPlayCard(PlayerRole role, string effectKey);
    void ValidateTargetRequired(string effectKey, Guid? targetUserId, Guid actorUserId);
    void ValidateCardNotDisabled(PlayerCardState hand, Guid cardId);
}

public sealed class CardPlayValidator : ICardPlayValidator
{
    private static readonly Dictionary<PlayerRole, HashSet<string>> AllowedEffects = new()
    {
        [PlayerRole.Human] = new(StringComparer.OrdinalIgnoreCase)
            { "shoot", "heal", "shield", "human_role", "pass" },
        [PlayerRole.Zombie] = new(StringComparer.OrdinalIgnoreCase)
            { "shield", "zombie_poison", "pass" },
        [PlayerRole.PowerZombie] = new(StringComparer.OrdinalIgnoreCase)
            { "zombie_poison", "pass" }
    };

    public void ValidateCardPlayable(Guid cardId)
    {
        if (RoleCardCatalog.IsRoleCard(cardId))
            throw new ServiceException("Role cards are for identity only and cannot be played.");
    }

    public void ValidateRoleCanPlayCard(PlayerRole role, string effectKey)
    {
        if (GameCombatRules.IsHumanPresenceAction(effectKey))
        {
            if (!GameCombatRules.CanPlayHumanPresenceAction(role))
                throw new ServiceException("Only humans can play the Visitor card.");
            return;
        }

        if (effectKey.Equals("pass", StringComparison.OrdinalIgnoreCase))
            return;

        if (!AllowedEffects.TryGetValue(role, out var allowed) || !allowed.Contains(effectKey))
            throw new ServiceException($"Role {role} cannot play card with effect '{effectKey}'.");
    }

    public void ValidateTargetRequired(string effectKey, Guid? targetUserId, Guid actorUserId)
    {
        if (effectKey.Equals("shield", StringComparison.OrdinalIgnoreCase) ||
            effectKey.Equals("human_role", StringComparison.OrdinalIgnoreCase) ||
            effectKey.Equals("pass", StringComparison.OrdinalIgnoreCase))
        {
            if (targetUserId is null || targetUserId != actorUserId)
                throw new ServiceException("This card must target yourself.");
            return;
        }

        if (effectKey.Equals("heal", StringComparison.OrdinalIgnoreCase))
        {
            // Heal cures a revealed infected player — must name a target (usually the battle opponent).
            if (targetUserId is null)
                throw new ServiceException("Heal requires a target.");
            if (targetUserId == actorUserId)
                throw new ServiceException("Heal must target an infected player, not yourself.");
            return;
        }

        if (targetUserId is null)
            throw new ServiceException("Target is required for this card.");
    }

    public void ValidateCardNotDisabled(PlayerCardState hand, Guid cardId)
    {
        if (hand.DisabledCardIds.Contains(cardId))
            throw new ServiceException("This card was disabled when you were infected.");
    }
}
