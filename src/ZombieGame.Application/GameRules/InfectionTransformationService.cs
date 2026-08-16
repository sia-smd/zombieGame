namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public sealed class InfectionTransformResult
{
    public int DisabledShotguns { get; init; }
    public int DisabledHeals { get; init; }
    public bool HasRemainingShieldCard { get; init; }
}

public sealed class InfectionTransformationService
{
    private readonly ICardRegistry _cards;

    public InfectionTransformationService(ICardRegistry cards) => _cards = cards;

    public InfectionTransformResult ApplyHumanToZombie(GameSessionState state, Guid playerId)
    {
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == playerId)
            ?? throw new InvalidOperationException("Player hand not found.");

        var disabledShotguns = 0;
        var disabledHeals = 0;
        var hasShield = false;

        hand.ClearInventorySlot(ActionCardCatalog.Visitor);
        hand.DisabledCardIds.Remove(ActionCardCatalog.Visitor);

        foreach (var cardId in hand.GetInventoryCardIds())
        {
            var card = _cards.GetById(cardId);
            if (card is null) continue;

            switch (card.EffectKey.ToLowerInvariant())
            {
                case "shoot":
                    hand.DisabledCardIds.Add(cardId);
                    disabledShotguns++;
                    break;
                case "heal":
                    hand.DisabledCardIds.Add(cardId);
                    disabledHeals++;
                    break;
                case "shield":
                    hasShield = true;
                    break;
            }
        }

        EnsurePermanentAction(hand, ActionCardCatalog.ZombiePoison);

        var player = state.GetPlayer(playerId);
        if (player is not null)
            hand.RoleCardId = RoleCardCatalog.GetRoleCardId(player.Role);

        return new InfectionTransformResult
        {
            DisabledShotguns = disabledShotguns,
            DisabledHeals = disabledHeals,
            HasRemainingShieldCard = hasShield
        };
    }

    public void RevertZombieToHuman(PlayerCardState hand)
    {
        hand.DisabledCardIds.Clear();
        hand.RoleCardId = RoleCardCatalog.Human;
        hand.ClearInventorySlot(ActionCardCatalog.ZombiePoison);
        hand.DisabledCardIds.Remove(ActionCardCatalog.ZombiePoison);
        EnsurePermanentAction(hand, ActionCardCatalog.Visitor);
        if (!hand.GetInventoryCardIds().Contains(ActionCardCatalog.Pass))
            EnsurePermanentAction(hand, ActionCardCatalog.Pass);
    }

    public void DemotePowerZombieToZombie(PlayerCardState hand)
    {
        hand.RoleCardId = RoleCardCatalog.Infection;
    }

    private static void EnsurePermanentAction(PlayerCardState hand, Guid cardId)
    {
        if (hand.GetInventoryCardIds().Contains(cardId))
            return;

        var emptySlot = hand.FirstEmptyInventorySlot();
        if (emptySlot is null)
            return;

        hand.SetInventorySlot(emptySlot.Value, cardId);
    }
}
