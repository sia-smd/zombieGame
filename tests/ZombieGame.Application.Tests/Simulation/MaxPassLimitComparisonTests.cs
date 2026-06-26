namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.Simulation;

public class MaxPassLimitComparisonTests
{
    [Fact]
    public async Task RunMaxPassLimitComparison_ProducesBothModes()
    {
        var service = new MatchSimulationService();
        var result = await service.RunMaxPassLimitComparisonAsync(new MatchSimulationOptions
        {
            MatchCount = 100,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = 4
        });

        Assert.Equal(100, result.UnlimitedPass.Balance.TotalMatches);
        Assert.Equal(100, result.MaxOnePassPerDay.Balance.TotalMatches);
    }

    [Fact]
    public async Task RunMaxPassLimitComparison_10k_GeneratesReport()
    {
        var service = new MatchSimulationService();
        var result = await service.RunMaxPassLimitComparisonAsync(new MatchSimulationOptions
        {
            MatchCount = 10_000,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = Environment.ProcessorCount
        });

        var report = MaxPassLimitComparisonFormatter.Format(result);

        Assert.Contains("Unlimited Pass", report);
        Assert.Contains("MaxPassPerDay=1", report);
        Assert.Contains("Human Passes/Match", report);
        Assert.Contains("Zombie Passes/Match", report);
        Assert.Contains("PowerZombie Passes/Match", report);
        Assert.Equal(10_000, result.UnlimitedPass.Balance.TotalMatches);
        Assert.Equal(10_000, result.MaxOnePassPerDay.Balance.TotalMatches);

        Console.WriteLine(report);
    }
}
