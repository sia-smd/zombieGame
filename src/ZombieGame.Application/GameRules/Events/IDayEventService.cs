namespace ZombieGame.Application.GameRules.Events;

using ZombieGame.Domain.Enums;

public interface IDayEventModifier
{
    DayEventType EventType { get; }
    int GetShotgunHitsRequired(PlayerRole role);
    bool CanPowerZombieAct { get; }
    bool CanPowerZombieBeKilled { get; }
}

public interface IDayEventService
{
    IDayEventModifier GetModifier(DayEventType eventType);
    DayEventType PickRandomEvent();
    int GetShotgunHitsRequired(PlayerRole role, DayEventType eventType);
    bool CanPowerZombieAct(DayEventType eventType);
    bool CanPowerZombieBeKilled(DayEventType eventType);
}
