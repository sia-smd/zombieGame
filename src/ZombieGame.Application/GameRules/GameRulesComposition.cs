namespace ZombieGame.Application.GameRules;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

/// <summary>
/// Wires game-rule services and card handlers from the canonical rules set.
/// </summary>
public static class GameRulesComposition
{
    public static IDayEventService CreateDayEventService(DayEventOptions? options = null) =>
        new DayEventService(Options.Create(options ?? new DayEventOptions()));

    public static InfectionTransformationService CreateTransformationService(ICardRegistry cardRegistry) =>
        new(cardRegistry);

    public static ICardEffectHandler[] CreateCardHandlers(
        IDayEventService dayEvents,
        InfectionTransformationService transformation,
        ICardRegistry cardRegistry,
        ICardConsumptionService consumption)
    {
        var heal = new HealHandler(transformation);
        return
        [
            new HumanRoleHandler(),
            new ShotgunHandler(dayEvents),
            heal,
            new ShieldHandler(),
            new ZombieInfectionHandler(transformation, cardRegistry, consumption, heal),
            new PowerZombieInfectionHandler(transformation, dayEvents, cardRegistry, consumption, heal)
        ];
    }

    public static CardEffectResolver CreateCardEffectResolver(
        IDayEventService dayEvents,
        InfectionTransformationService transformation,
        ICardRegistry cardRegistry,
        ICardConsumptionService consumption) =>
        new(CreateCardHandlers(dayEvents, transformation, cardRegistry, consumption), dayEvents);

    public static CardEffectResolver CreateCardEffectResolver(
        IDayEventService dayEvents,
        InfectionTransformationService transformation) =>
        CreateCardEffectResolver(dayEvents, transformation, new NullCardRegistry(), new CardConsumptionService());

    private sealed class NullCardRegistry : ICardRegistry
    {
        public CardDefinition? GetById(Guid id) => null;
        public IReadOnlyList<CardDefinition> GetAll() => [];
        public IReadOnlyList<CardDefinition> GetByType(Domain.Enums.CardType type) => [];
    }

    public static void RegisterDayEvents(IServiceCollection services)
    {
        services.AddSingleton<IDayEventService, DayEventService>();
    }
}
