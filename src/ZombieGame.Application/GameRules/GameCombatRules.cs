namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

/// <summary>
/// Single source of truth for combat and card-interaction rules.
/// Used by live matches, simulators, and tests.
/// </summary>
public static class GameCombatRules
{
    public const int PriorityShield = 1;
    public const int PriorityHeal = 2;
    public const int PriorityShoot = 3;
    public const int PriorityInfect = 4;
    public const int PriorityPass = 100;

    public static int GetEffectPriority(string effectKey) => effectKey.ToLowerInvariant() switch
    {
        "shield" => PriorityShield,
        "heal" => PriorityHeal,
        "shoot" => PriorityShoot,
        "infect" or "power_zombie" or "zombie_poison" => PriorityInfect,
        "human_role" => PriorityPass,
        "pass" => PriorityPass,
        _ => 50
    };

    public static IEnumerable<T> OrderByEffectPriority<T>(
        IEnumerable<T> items,
        Func<T, string> effectKey,
        Func<T, int> sequence) =>
        items
            .OrderBy(i => GetEffectPriority(effectKey(i)))
            .ThenBy(sequence);

    /// <summary>
    /// Defensive priority: a human holding shield with remaining actions blocks infection
    /// even if shield is played later in the turn.
    /// </summary>
    public static bool InfectionBlockedByShieldPriority(
        GamePlayerState target,
        PlayerCardState? hand,
        Func<Guid, CardDefinition?> resolveCard) =>
        target.Role == PlayerRole.Human &&
        target.IsAlive &&
        !target.HasShield &&
        target.RemainingActions > 0 &&
        hand is not null &&
        hand.HasAvailableEffect(resolveCard, "shield");

    /// <summary>
    /// Passing on the first action of a turn ends the turn immediately (both action slots count as pass).
    /// </summary>
    public static bool IsFirstActionOfTurn(GamePlayerState actor) =>
        actor.ActionsUsedThisTurn == 0;

    public static void CompleteTurnAfterFirstPass(GamePlayerState actor)
    {
        actor.ActionsUsedThisTurn = actor.ActionsPerTurn;
    }

    public static bool CanTakeAction(GamePlayerState actor) =>
        actor.IsAlive && actor.RemainingActions > 0;

    /// <summary>
    /// Humans may play their Visitor card to spend an action with no combat effect.
    /// This is not a pass and does not end the turn early.
    /// </summary>
    public static bool IsHumanPresenceAction(string effectKey) =>
        effectKey.Equals("human_role", StringComparison.OrdinalIgnoreCase);

    public static bool CanPlayHumanPresenceAction(PlayerRole role) =>
        role == PlayerRole.Human;

    public static bool CanPowerZombieAttack(DayEventType dayEvent) =>
        dayEvent != DayEventType.SunnyDay;

    public static bool CanInfectedRoleAttackToday(PlayerRole role, DayEventType dayEvent) =>
        role == PlayerRole.Zombie || CanPowerZombieAttack(dayEvent);

    /// <summary>Shield blocks PowerZombie infection only during storm days.</summary>
    public static bool DoesShieldBlockPowerZombieInfection(DayEventType dayEvent) =>
        dayEvent == DayEventType.Storm;

    public static bool CanPowerZombieBeKilledByShotgun(DayEventType dayEvent) =>
        dayEvent != DayEventType.Storm;

    public static int GetShotgunHitsRequired(PlayerRole role, DayEventType dayEvent) => (role, dayEvent) switch
    {
        (PlayerRole.PowerZombie, DayEventType.Storm) => int.MaxValue,
        (PlayerRole.PowerZombie, _) => 2,
        (PlayerRole.Zombie, DayEventType.Storm) => 2,
        (PlayerRole.Zombie, _) => 1,
        _ => 1
    };

    /// <summary>
    /// Heal only affects infected players who attacked (infected someone) this day.
    /// Players who pass and stay hidden cannot be healed.
    /// </summary>
    public static bool CanHealTarget(PlayerRole role, bool hasRevealedThisDay) =>
        hasRevealedThisDay && role is PlayerRole.Zombie or PlayerRole.PowerZombie;

    public static bool CanHealTarget(GamePlayerState target) =>
        CanHealTarget(target.Role, target.HasRevealedThisDay);

    public static PlayerRole? GetHealResultRole(PlayerRole currentRole) => currentRole switch
    {
        PlayerRole.Zombie => PlayerRole.Human,
        PlayerRole.PowerZombie => PlayerRole.Zombie,
        _ => null
    };
}
