namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.Simulation;

public class MatchSimulationServiceTests
{
    [Fact]
    public async Task RunAsync_CompletesRequestedMatches()
    {
        var service = new MatchSimulationService();
        var result = await service.RunAsync(new MatchSimulationOptions
        {
            MatchCount = 100,
            PlayerCount = 8,
            MaxTurnsPerMatch = 30,
            RandomSeed = 42,
            MaxParallelism = 4
        });

        Assert.Equal(100, result.Balance.TotalMatches);
        Assert.Equal(100, result.Balance.HumanWins + result.Balance.ZombieWins + result.Balance.Stalemates);
        Assert.True(result.Balance.HumanWinRate + result.Balance.ZombieWinRate <= 100.1);
        Assert.True(result.Elapsed.TotalSeconds >= 0);
        Assert.True(result.MatchesPerSecond > 0);
    }

    [Fact]
    public async Task RunAsync_ProducesBalanceMetrics()
    {
        var service = new MatchSimulationService();
        var result = await service.RunAsync(new MatchSimulationOptions
        {
            MatchCount = 50,
            PlayerCount = 8,
            RandomSeed = 123
        });

        Assert.True(result.Balance.AverageTurns > 0);
        Assert.NotEmpty(result.Balance.CardPlaysByEffect);
        Assert.Contains("PowerZombie", result.Balance.WinRateWhenRolePresentAtStart.Keys);
    }

    [Fact]
    public async Task RunAsync_10k_Matches_CompletesInReasonableTime()
    {
        var service = new MatchSimulationService();
        var result = await service.RunAsync(new MatchSimulationOptions
        {
            MatchCount = 10_000,
            PlayerCount = 8,
            MaxParallelism = Environment.ProcessorCount,
            RandomSeed = 999
        });

        Assert.Equal(10_000, result.Balance.TotalMatches);
        Assert.True(result.Elapsed.TotalMinutes < 5, $"10k simulations took {result.Elapsed.TotalSeconds}s");
    }
}
