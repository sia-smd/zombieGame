namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;

public interface ICardRegistry
{
    IReadOnlyList<CardDefinition> GetAll();
    CardDefinition? GetById(Guid id);
    IReadOnlyList<CardDefinition> GetByType(CardType type);
}
