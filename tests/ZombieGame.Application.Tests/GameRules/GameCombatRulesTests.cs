namespace ZombieGame.Application.Tests.GameRules;

using ZombieGame.Application.GameRules;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public class GameCombatRulesTests
{
    [Theory]
    [InlineData("shield", 1)]
    [InlineData("heal", 2)]
    [InlineData("shoot", 3)]
    [InlineData("infect", 4)]
    [InlineData("power_zombie", 4)]
    public void GetEffectPriority_OrdersCombatEffects(string effectKey, int expected) =>
        Assert.Equal(expected, GameCombatRules.GetEffectPriority(effectKey));

    [Theory]
    [InlineData(DayEventType.SunnyDay, false)]
    [InlineData(DayEventType.NormalDay, true)]
    [InlineData(DayEventType.Storm, true)]
    public void CanPowerZombieAttack_RespectsDayEvent(DayEventType dayEvent, bool expected) =>
        Assert.Equal(expected, GameCombatRules.CanPowerZombieAttack(dayEvent));

    [Theory]
    [InlineData(DayEventType.NormalDay, false)]
    [InlineData(DayEventType.SunnyDay, false)]
    [InlineData(DayEventType.Storm, true)]
    public void DoesShieldBlockPowerZombieInfection_OnlyOnStorm(DayEventType dayEvent, bool expected) =>
        Assert.Equal(expected, GameCombatRules.DoesShieldBlockPowerZombieInfection(dayEvent));

    [Theory]
    [InlineData(PlayerRole.Zombie, DayEventType.NormalDay, 1)]
    [InlineData(PlayerRole.Zombie, DayEventType.Storm, 2)]
    [InlineData(PlayerRole.PowerZombie, DayEventType.NormalDay, 2)]
    public void GetShotgunHitsRequired_RespectsStorm(PlayerRole role, DayEventType dayEvent, int expected) =>
        Assert.Equal(expected, GameCombatRules.GetShotgunHitsRequired(role, dayEvent));

    [Fact]
    public void GetShotgunHitsRequired_PowerZombieImmuneOnStorm() =>
        Assert.Equal(int.MaxValue, GameCombatRules.GetShotgunHitsRequired(PlayerRole.PowerZombie, DayEventType.Storm));

    [Fact]
    public void CanHealTarget_RequiresAttackReveal()
    {
        var revealedZombie = new GamePlayerState { Role = PlayerRole.Zombie, HasRevealedThisDay = true };
        var hiddenZombie = new GamePlayerState { Role = PlayerRole.Zombie, HasRevealedThisDay = false };
        var revealedPower = new GamePlayerState { Role = PlayerRole.PowerZombie, HasRevealedThisDay = true };

        Assert.True(GameCombatRules.CanHealTarget(revealedZombie));
        Assert.False(GameCombatRules.CanHealTarget(hiddenZombie));
        Assert.True(GameCombatRules.CanHealTarget(revealedPower));
    }

    [Theory]
    [InlineData(PlayerRole.Zombie, PlayerRole.Human)]
    [InlineData(PlayerRole.PowerZombie, PlayerRole.Zombie)]
    public void GetHealResultRole_MapsRoles(PlayerRole current, PlayerRole expected) =>
        Assert.Equal(expected, GameCombatRules.GetHealResultRole(current));

    [Fact]
    public void CompleteTurnAfterFirstPass_UsesAllActionSlots()
    {
        var player = new GamePlayerState { ActionsPerTurn = 2, ActionsUsedThisTurn = 0 };

        GameCombatRules.CompleteTurnAfterFirstPass(player);

        Assert.Equal(2, player.ActionsUsedThisTurn);
        Assert.Equal(0, player.RemainingActions);
    }
}
