namespace ZombieGame.Infrastructure.Cards;

using Microsoft.EntityFrameworkCore;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure.Persistence;

public class CardRegistry : ICardRegistry
{
    private readonly ApplicationDbContext _context;
    private IReadOnlyList<CardDefinition>? _cache;

    public CardRegistry(ApplicationDbContext context) => _context = context;

    public IReadOnlyList<CardDefinition> GetAll() => Load();

    public CardDefinition? GetById(Guid id) => Load().FirstOrDefault(c => c.Id == id);

    public IReadOnlyList<CardDefinition> GetByType(CardType type) =>
        Load().Where(c => c.Type == type).ToList();

    private IReadOnlyList<CardDefinition> Load()
    {
        _cache ??= _context.CardDefinitions.AsNoTracking().Where(c => c.IsActive).ToList();
        return _cache;
    }

    public static IReadOnlyList<CardDefinition> GetSeedData() =>
    [
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111101"), Name = "Infection", Type = CardType.Zombie, Description = "Spread infection.", EffectKey = "infect" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111102"), Name = "Survivor", Type = CardType.Human, Description = "Human resilience.", EffectKey = "survive" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111103"), Name = "Shotgun", Type = CardType.Shotgun, Description = "Eliminate a threat.", EffectKey = "shoot" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111104"), Name = "Medkit", Type = CardType.Heal, Description = "Restore health.", EffectKey = "heal" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111105"), Name = "Barricade", Type = CardType.Shield, Description = "Block an attack.", EffectKey = "shield" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111106"), Name = "Alpha Zombie", Type = CardType.PowerZombie, Description = "Empowered zombie.", EffectKey = "power_zombie" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111107"), Name = "Blackout", Type = CardType.EventCard, Description = "Global event.", EffectKey = "blackout" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111108"), Name = "Human", Type = CardType.Human, Description = "Human role identity.", EffectKey = "human_role" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111109"), Name = "Visitor", Type = CardType.Human, Description = "Human presence action.", EffectKey = "human_role" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111110"), Name = "Zombie Poison", Type = CardType.Zombie, Description = "Infected attack action.", EffectKey = "zombie_poison" },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Pass", Type = CardType.EventCard, Description = "Skip the rest of your turn.", EffectKey = "pass" }
    ];
}
