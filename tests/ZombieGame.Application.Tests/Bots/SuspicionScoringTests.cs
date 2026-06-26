namespace ZombieGame.Application.Tests.Bots;

using ZombieGame.Application.Bots;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;

public class SuspicionScoringTests
{
    [Fact]
    public void RecordPass_IncreasesSuspicion()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((a, PlayerRole.Human, true), (b, PlayerRole.Human, true));

        SuspicionScoring.EnsureInitialized(state);
        SuspicionScoring.RecordPass(state, b);

        Assert.True(SuspicionScoring.GetSuspicion(state, a, b) > 0);
    }

    [Fact]
    public void PickHighestSuspicionTarget_SelectsMostSuspiciousPlayer()
    {
        var voter = Guid.NewGuid();
        var low = Guid.NewGuid();
        var high = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (voter, PlayerRole.Human, true),
            (low, PlayerRole.Human, true),
            (high, PlayerRole.Zombie, true));

        SuspicionScoring.EnsureInitialized(state);
        SuspicionScoring.AddPublicSuspicion(state, high, 5);
        SuspicionScoring.AddPublicSuspicion(state, low, 1);

        var target = SuspicionScoring.PickHighestSuspicionTarget(state, voter);

        Assert.Equal(high, target);
    }
}
