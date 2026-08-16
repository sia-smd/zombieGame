namespace ZombieGame.Application.Simulation;

using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public sealed class InMemoryCardRegistry : ICardRegistry
{
    private static readonly IReadOnlyList<CardDefinition> Cards =
    [
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111101"), Name = "Infection", Type = CardType.Zombie, EffectKey = "infect", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111102"), Name = "Survivor", Type = CardType.Human, EffectKey = "survive", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111103"), Name = "Shotgun", Type = CardType.Shotgun, EffectKey = "shoot", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111104"), Name = "Medkit", Type = CardType.Heal, EffectKey = "heal", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111105"), Name = "Barricade", Type = CardType.Shield, EffectKey = "shield", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111106"), Name = "Alpha Zombie", Type = CardType.PowerZombie, EffectKey = "power_zombie", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111107"), Name = "Blackout", Type = CardType.EventCard, EffectKey = "blackout", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111108"), Name = "Human", Type = CardType.Human, EffectKey = "human_role", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111109"), Name = "Visitor", Type = CardType.Human, EffectKey = "human_role", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111110"), Name = "Zombie Poison", Type = CardType.Zombie, EffectKey = "zombie_poison", IsActive = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Pass", Type = CardType.EventCard, EffectKey = "pass", IsActive = true }
    ];

    public IReadOnlyList<CardDefinition> GetAll() => Cards;

    public CardDefinition? GetById(Guid id) => Cards.FirstOrDefault(c => c.Id == id);

    public IReadOnlyList<CardDefinition> GetByType(CardType type) =>
        Cards.Where(c => c.Type == type).ToList();
}
