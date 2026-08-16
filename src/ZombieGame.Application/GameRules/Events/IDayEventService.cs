namespace ZombieGame.Application.GameRules.Events;

using ZombieGame.Domain.Enums;

public interface IDayEventService
{
    DayEventType PickRandomEvent();
    int GetShotgunHitsRequired(PlayerRole role, DayEventType eventType);
    bool CanPowerZombieAct(DayEventType eventType);
    bool CanPowerZombieBeKilled(DayEventType eventType);
    bool DoesShieldBlockPowerZombieInfection(DayEventType eventType);
}
