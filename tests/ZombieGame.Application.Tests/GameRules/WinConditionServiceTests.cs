namespace ZombieGame.Application.Tests.GameRules;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;

public class WinConditionServiceTests
{
    private readonly WinConditionService _winConditions = new();

    [Fact]
    public void HumansWin_WhenAllZombiesDead()
    {
        var state = GameTestBuilder.CreateSession(
            (Guid.NewGuid(), PlayerRole.Human, true),
            (Guid.NewGuid(), PlayerRole.Human, true));

        var result = _winConditions.Evaluate(state);
        Assert.Equal(WinTeam.Humans, result);
    }

    [Fact]
    public void ZombiesWin_WhenNoHumansAlive()
    {
        var state = GameTestBuilder.CreateSession(
            (Guid.NewGuid(), PlayerRole.Zombie, true),
            (Guid.NewGuid(), PlayerRole.PowerZombie, true));

        var result = _winConditions.Evaluate(state);
        Assert.Equal(WinTeam.Zombies, result);
    }

    [Fact]
    public void NoWinner_WhenBothTeamsAlive()
    {
        var state = GameTestBuilder.CreateSession(
            (Guid.NewGuid(), PlayerRole.Human, true),
            (Guid.NewGuid(), PlayerRole.Zombie, true));

        var result = _winConditions.Evaluate(state);
        Assert.Null(result);
    }
}
