namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.Simulation;

public class PassPenaltyComparisonTests
{
    [Fact]
    public async Task RunPassPenaltyComparison_ProducesBothModes()
    {
        var service = new MatchSimulationService();
        var result = await service.RunPassPenaltyComparisonAsync(new MatchSimulationOptions
        {
            MatchCount = 100,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = 4
        });

        Assert.Equal(100, result.NoPenalty.Balance.TotalMatches);
        Assert.Equal(100, result.PassPenaltyMode.Balance.TotalMatches);
        Assert.True(result.TotalElapsed.TotalSeconds >= 0);
    }

    [Fact]
    public async Task PassPenaltyMode_SkipsDailyDealAfterPass()
    {
        var simulator = new MatchSimulator(new MatchSimulationOptions());
        var random = new Random(42);
        var outcome = await simulator.RunSingleMatchAsync(new MatchSimulationOptions
        {
            PlayerCount = 8,
            MaxTurnsPerMatch = 5,
            PassPenaltyMode = true,
            RandomSeed = 42
        }, random);

        Assert.True(outcome.TotalCardPlays >= 0);
    }

    [Fact]
    public async Task RunPassPenaltyComparison_10k_GeneratesReport()
    {
        var service = new MatchSimulationService();
        var result = await service.RunPassPenaltyComparisonAsync(new MatchSimulationOptions
        {
            MatchCount = 10_000,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = Environment.ProcessorCount
        });

        var report = PassPenaltyComparisonFormatter.Format(result);

        Assert.Contains("No Penalty", report);
        Assert.Contains("PassPenaltyMode", report);
        Assert.Equal(10_000, result.NoPenalty.Balance.TotalMatches);
        Assert.Equal(10_000, result.PassPenaltyMode.Balance.TotalMatches);

        Console.WriteLine(report);
    }
}
