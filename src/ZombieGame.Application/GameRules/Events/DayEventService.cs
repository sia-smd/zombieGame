namespace ZombieGame.Application.GameRules.Events;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;

public class DayEventService : IDayEventService
{
    private readonly DayEventOptions _options;
    private readonly Random _random;

    public DayEventService(IOptions<DayEventOptions> options, Random? random = null)
    {
        _options = options.Value;
        _random = random ?? Random.Shared;
    }

    public DayEventType PickRandomEvent()
    {
        var total = _options.TotalWeight;
        if (total <= 0)
            return DayEventType.NormalDay;

        var roll = _random.Next(total);
        if (roll < _options.NormalWeight)
            return DayEventType.NormalDay;
        if (roll < _options.NormalWeight + _options.SunnyWeight)
            return DayEventType.SunnyDay;
        return DayEventType.Storm;
    }

    public int GetShotgunHitsRequired(PlayerRole role, DayEventType eventType) =>
        GameCombatRules.GetShotgunHitsRequired(role, eventType);

    public bool CanPowerZombieAct(DayEventType eventType) =>
        GameCombatRules.CanPowerZombieAttack(eventType);

    public bool CanPowerZombieBeKilled(DayEventType eventType) =>
        GameCombatRules.CanPowerZombieBeKilledByShotgun(eventType);

    public bool DoesShieldBlockPowerZombieInfection(DayEventType eventType) =>
        GameCombatRules.DoesShieldBlockPowerZombieInfection(eventType);
}
