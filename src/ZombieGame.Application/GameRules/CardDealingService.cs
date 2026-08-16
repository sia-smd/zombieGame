namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public interface ICardDealingService
{
    void InitializeHands(GameSessionState state);
    void ReplenishInventory(GameSessionState state);
}

public sealed class CardDealingService : ICardDealingService
{
    private readonly IInventoryCardGenerator _generator;
    private readonly Options.GameSettings _settings;

    public CardDealingService(
        IInventoryCardGenerator generator,
        Microsoft.Extensions.Options.IOptions<Options.GameSettings> settings)
    {
        _generator = generator;
        _settings = settings.Value;
    }

    public void InitializeHands(GameSessionState state)
    {
        foreach (var player in state.AlivePlayers)
            SetupHand(state, player);
    }

    public void ReplenishInventory(GameSessionState state)
    {
        foreach (var player in state.AlivePlayers)
        {
            if (player.InactiveForNextDealing)
            {
                player.InactiveForNextDealing = false;
                continue;
            }

            FillEmptySlots(state, player);
        }
    }

    private void SetupHand(GameSessionState state, GamePlayerState player)
    {
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == player.UserId);
        if (hand is null)
            return;

        hand.RoleCardId = RoleCardCatalog.GetRoleCardId(player.Role);
        hand.InventorySlot1 = ActionCardCatalog.GetRoleActionFor(player.Role) ?? ActionCardCatalog.Visitor;
        hand.InventorySlot2 = ActionCardCatalog.Pass;
        hand.InventorySlot3 = null;
        hand.InventorySlot4 = null;

        FillEmptySlots(state, player);
    }

    private void FillEmptySlots(GameSessionState state, GamePlayerState player)
    {
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == player.UserId);
        if (hand is null)
            return;

        var maxSlots = Math.Clamp(_settings.Inventory?.MaxSlots ?? 4, 2, PlayerHandExtensions.InventorySlotCount);
        for (var slot = 0; slot < maxSlots; slot++)
        {
            if (hand.GetInventorySlot(slot) is not null)
                continue;

            hand.SetInventorySlot(slot, _generator.GenerateForHand(hand, player.Role));
        }
    }
}
