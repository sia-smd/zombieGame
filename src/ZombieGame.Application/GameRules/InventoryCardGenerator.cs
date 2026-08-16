namespace ZombieGame.Application.GameRules;

using ZombieGame.Application.Options;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public interface IInventoryCardGenerator
{
    Guid GenerateForHand(PlayerCardState hand, PlayerRole role, Random? random = null);
}

public sealed class InventoryCardGenerator : IInventoryCardGenerator
{
    private const int MaxRerolls = 25;

    private readonly ICardRegistry _cardRegistry;
    private readonly GameSettings _settings;
    private CardDefinition? _shotgun;
    private CardDefinition? _heal;
    private CardDefinition? _shield;

    public InventoryCardGenerator(ICardRegistry cardRegistry, Microsoft.Extensions.Options.IOptions<GameSettings> settings)
    {
        _cardRegistry = cardRegistry;
        _settings = settings.Value;
    }

    public Guid GenerateForHand(PlayerCardState hand, PlayerRole role, Random? random = null)
    {
        random ??= Random.Shared;
        EnsureInventoryCardsLoaded();

        for (var attempt = 0; attempt < MaxRerolls; attempt++)
        {
            var card = PickWeighted(random, role);
            if (CanAddToInventory(hand, card))
                return card.Id;
        }

        return role == PlayerRole.Human ? _shotgun!.Id : _shield!.Id;
    }

    private CardDefinition PickWeighted(Random random, PlayerRole role)
    {
        EnsureInventoryCardsLoaded();
        var weights = _settings.CardDistribution ?? new CardDistributionSettings();
        var total = weights.Shotgun + weights.Heal + weights.Shield;
        if (total <= 0)
            return _shotgun!;

        var roll = random.Next(total);
        if (roll < weights.Shotgun)
            return _shotgun!;
        roll -= weights.Shotgun;
        if (roll < weights.Heal)
            return _heal!;
        return _shield!;
    }

    private bool CanAddToInventory(PlayerCardState hand, CardDefinition card)
    {
        EnsureInventoryCardsLoaded();

        if (RoleCardCatalog.IsRoleCard(card.Id) || ActionCardCatalog.IsPermanentInventoryCard(card.Id))
            return false;

        var inventory = hand.GetInventoryCardIds().ToList();
        var maxShield = _settings.Inventory?.MaxShield ?? 1;
        var maxHeal = _settings.Inventory?.MaxHeal ?? 1;

        return card.EffectKey switch
        {
            "shield" => inventory.Count(id => id == _shield!.Id) < maxShield,
            "heal" => inventory.Count(id => id == _heal!.Id) < maxHeal,
            "shoot" => true,
            _ => false
        };
    }

    private void EnsureInventoryCardsLoaded()
    {
        if (_shotgun is not null)
            return;

        var all = _cardRegistry.GetAll();
        _shotgun = all.FirstOrDefault(c => c.EffectKey == "shoot")
            ?? throw new InvalidOperationException("Shotgun card not found in registry.");
        _heal = all.FirstOrDefault(c => c.EffectKey == "heal")
            ?? throw new InvalidOperationException("Heal card not found in registry.");
        _shield = all.FirstOrDefault(c => c.EffectKey == "shield")
            ?? throw new InvalidOperationException("Shield card not found in registry.");
    }
}
