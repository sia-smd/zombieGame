namespace ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

public class CardDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CardType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string EffectKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
