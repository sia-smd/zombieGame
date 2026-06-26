namespace ZombieGame.Application.GameRules.Events;

using ZombieGame.Domain.Enums;

public class NormalDayModifier : IDayEventModifier
{
    public DayEventType EventType => DayEventType.NormalDay;
    public bool CanPowerZombieAct => true;
    public bool CanPowerZombieBeKilled => true;

    public int GetShotgunHitsRequired(PlayerRole role) => role switch
    {
        PlayerRole.Zombie => 1,
        PlayerRole.PowerZombie => 2,
        _ => 1
    };
}

public class SunnyDayModifier : IDayEventModifier
{
    public DayEventType EventType => DayEventType.SunnyDay;
    public bool CanPowerZombieAct => false;
    public bool CanPowerZombieBeKilled => true;

    public int GetShotgunHitsRequired(PlayerRole role) => role switch
    {
        PlayerRole.Zombie => 1,
        PlayerRole.PowerZombie => 2,
        _ => 1
    };
}

public class StormDayModifier : IDayEventModifier
{
    public DayEventType EventType => DayEventType.Storm;
    public bool CanPowerZombieAct => true;
    public bool CanPowerZombieBeKilled => false;

    public int GetShotgunHitsRequired(PlayerRole role) => role switch
    {
        PlayerRole.Zombie => 2,
        PlayerRole.PowerZombie => int.MaxValue,
        _ => 1
    };
}

public class DayEventService : IDayEventService
{
    private readonly IReadOnlyDictionary<DayEventType, IDayEventModifier> _modifiers;

    public DayEventService(IEnumerable<IDayEventModifier> modifiers)
    {
        _modifiers = modifiers.ToDictionary(m => m.EventType);
    }

    public IDayEventModifier GetModifier(DayEventType eventType) =>
        _modifiers[eventType];

    public DayEventType PickRandomEvent()
    {
        var events = new[] { DayEventType.NormalDay, DayEventType.SunnyDay, DayEventType.Storm };
        return events[Random.Shared.Next(events.Length)];
    }

    public int GetShotgunHitsRequired(PlayerRole role, DayEventType eventType) =>
        GetModifier(eventType).GetShotgunHitsRequired(role);

    public bool CanPowerZombieAct(DayEventType eventType) =>
        GetModifier(eventType).CanPowerZombieAct;

    public bool CanPowerZombieBeKilled(DayEventType eventType) =>
        GetModifier(eventType).CanPowerZombieBeKilled;
}
