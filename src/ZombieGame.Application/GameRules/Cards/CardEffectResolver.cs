namespace ZombieGame.Application.GameRules.Cards;

using ZombieGame.Application.Common;
using ZombieGame.Application.GameRules.Events;
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
        if (context.Actor.Role == PlayerRole.PowerZombie &&
            context.Card.EffectKey is "infect" or "power_zombie" &&
            !_dayEvents.CanPowerZombieAct(context.State.CurrentDayEvent))
        {
            throw new ServiceException("PowerZombie cannot perform actions during SunnyDay.");
        }

        var handler = Resolve(context.Card.EffectKey);

        if (!handler.CanPlay(context))
            throw new ServiceException($"Cannot play {context.Card.Name} in the current situation.");

        RevealActorIfAttackCard(context);

        return handler.Apply(context);
    }

    private static void RevealActorIfAttackCard(CardEffectContext context)
    {
        if (context.Card.EffectKey is "infect" or "power_zombie")
            context.Actor.HasRevealedThisDay = true;
    }
}

public interface ICardPlayValidator
{
    void ValidateRoleCanPlayCard(PlayerRole role, string effectKey);
    void ValidateTargetRequired(string effectKey, Guid? targetUserId, Guid actorUserId);
    void ValidateCardNotDisabled(PlayerCardState hand, Guid cardId);
}

public sealed class CardPlayValidator : ICardPlayValidator
{
    private static readonly Dictionary<PlayerRole, HashSet<string>> AllowedEffects = new()
    {
        [PlayerRole.Human] = new(StringComparer.OrdinalIgnoreCase) { "shoot", "heal", "shield" },
        [PlayerRole.Zombie] = new(StringComparer.OrdinalIgnoreCase) { "infect", "shield" },
        [PlayerRole.PowerZombie] = new(StringComparer.OrdinalIgnoreCase) { "power_zombie" }
    };

    public void ValidateRoleCanPlayCard(PlayerRole role, string effectKey)
    {
        if (!AllowedEffects.TryGetValue(role, out var allowed) || !allowed.Contains(effectKey))
            throw new ServiceException($"Role {role} cannot play card with effect '{effectKey}'.");
    }

    public void ValidateTargetRequired(string effectKey, Guid? targetUserId, Guid actorUserId)
    {
        if (effectKey.Equals("shield", StringComparison.OrdinalIgnoreCase))
        {
            if (targetUserId is null || targetUserId != actorUserId)
                throw new ServiceException("Shield must target yourself.");
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
