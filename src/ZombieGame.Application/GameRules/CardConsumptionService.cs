namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Models;

public interface ICardConsumptionService
{
    void ConsumeAfterPlay(PlayerCardState hand, CardDefinition card, int? inventorySlotIndex = null);
}

public sealed class CardConsumptionService : ICardConsumptionService
{
    public void ConsumeAfterPlay(PlayerCardState hand, CardDefinition card, int? inventorySlotIndex = null)
    {
        if (RoleCardCatalog.IsRoleCard(card.Id))
            return;

        // Visitor, Poison, Pass, and Shield stay in hand permanently.
        if (ActionCardCatalog.IsPermanentInventoryCard(card.Id))
            return;

        if (card.EffectKey.Equals("shield", StringComparison.OrdinalIgnoreCase))
            return;

        if (inventorySlotIndex is int slot)
            hand.SetInventorySlot(slot, null);
        else
            hand.ClearInventorySlot(card.Id);

        hand.DisabledCardIds.Remove(card.Id);
    }
}
