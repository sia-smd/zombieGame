namespace ZombieGame.Domain.Models;

using ZombieGame.Domain.Entities;

public static class PlayerHandExtensions
{
    public const int InventorySlotCount = 4;

    public static IEnumerable<Guid> GetInventoryCardIds(this PlayerCardState hand)
    {
        if (hand.InventorySlot1 is Guid slot1)
            yield return slot1;
        if (hand.InventorySlot2 is Guid slot2)
            yield return slot2;
        if (hand.InventorySlot3 is Guid slot3)
            yield return slot3;
        if (hand.InventorySlot4 is Guid slot4)
            yield return slot4;
    }

    public static int InventoryCount(this PlayerCardState hand) =>
        GetInventoryCardIds(hand).Count();

    public static bool ContainsCard(this PlayerCardState hand, Guid cardId) =>
        hand.RoleCardId == cardId ||
        hand.InventorySlot1 == cardId ||
        hand.InventorySlot2 == cardId ||
        hand.InventorySlot3 == cardId ||
        hand.InventorySlot4 == cardId;

    public static bool HasAvailableEffect(
        this PlayerCardState hand,
        Func<Guid, CardDefinition?> resolveCard,
        string effectKey)
    {
        foreach (var cardId in hand.GetInventoryCardIds())
        {
            if (hand.DisabledCardIds.Contains(cardId))
                continue;

            var card = resolveCard(cardId);
            if (card?.EffectKey.Equals(effectKey, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        return false;
    }

    public static CardDefinition? FindAvailableEffectCard(
        this PlayerCardState hand,
        Func<Guid, CardDefinition?> resolveCard,
        string effectKey)
    {
        foreach (var cardId in hand.GetInventoryCardIds())
        {
            if (hand.DisabledCardIds.Contains(cardId))
                continue;

            var card = resolveCard(cardId);
            if (card?.EffectKey.Equals(effectKey, StringComparison.OrdinalIgnoreCase) == true)
                return card;
        }

        return null;
    }

    public static IEnumerable<Guid> EnumerateAllCards(this PlayerCardState hand)
    {
        if (hand.RoleCardId != Guid.Empty)
            yield return hand.RoleCardId;
        foreach (var id in hand.GetInventoryCardIds())
            yield return id;
    }

    public static int? FindInventorySlotIndex(this PlayerCardState hand, Guid cardId)
    {
        if (hand.InventorySlot1 == cardId)
            return 0;
        if (hand.InventorySlot2 == cardId)
            return 1;
        if (hand.InventorySlot3 == cardId)
            return 2;
        if (hand.InventorySlot4 == cardId)
            return 3;
        return null;
    }

    public static void SetInventorySlot(this PlayerCardState hand, int slotIndex, Guid? cardId)
    {
        switch (slotIndex)
        {
            case 0:
                hand.InventorySlot1 = cardId;
                break;
            case 1:
                hand.InventorySlot2 = cardId;
                break;
            case 2:
                hand.InventorySlot3 = cardId;
                break;
            case 3:
                hand.InventorySlot4 = cardId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
    }

    public static Guid? GetInventorySlot(this PlayerCardState hand, int slotIndex) => slotIndex switch
    {
        0 => hand.InventorySlot1,
        1 => hand.InventorySlot2,
        2 => hand.InventorySlot3,
        3 => hand.InventorySlot4,
        _ => throw new ArgumentOutOfRangeException(nameof(slotIndex))
    };

    public static int? FirstEmptyInventorySlot(this PlayerCardState hand)
    {
        for (var i = 0; i < InventorySlotCount; i++)
        {
            if (hand.GetInventorySlot(i) is null)
                return i;
        }

        return null;
    }

    public static void ClearInventorySlot(this PlayerCardState hand, Guid cardId)
    {
        if (hand.InventorySlot1 == cardId)
            hand.InventorySlot1 = null;
        if (hand.InventorySlot2 == cardId)
            hand.InventorySlot2 = null;
        if (hand.InventorySlot3 == cardId)
            hand.InventorySlot3 = null;
        if (hand.InventorySlot4 == cardId)
            hand.InventorySlot4 = null;
    }
}
