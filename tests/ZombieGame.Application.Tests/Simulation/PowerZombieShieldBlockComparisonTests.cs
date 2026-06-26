namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.Simulation;

public class PowerZombieShieldBlockComparisonTests
{
    [Fact]
    public async Task RunPowerZombieShieldBlockComparison_ProducesBothModes()
    {
        var service = new MatchSimulationService();
        var result = await service.RunPowerZombieShieldBlockComparisonAsync(new MatchSimulationOptions
        {
            MatchCount = 100,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = 4
        });

        Assert.Equal(100, result.CurrentRules.Balance.TotalMatches);
        Assert.Equal(100, result.ShieldBlocksPowerZombie.Balance.TotalMatches);
    }

    [Fact]
    public async Task RunPowerZombieShieldBlockComparison_10k_GeneratesReport()
    {
        var service = new MatchSimulationService();
        var result = await service.RunPowerZombieShieldBlockComparisonAsync(new MatchSimulationOptions
        {
            MatchCount = 10_000,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = Environment.ProcessorCount
        });

        var report = PowerZombieShieldBlockComparisonFormatter.Format(result);

        Assert.Contains("Current Rules", report);
        Assert.Contains("Shield Blocks PowerZombie", report);
        Assert.Contains("PowerZombie Kill Rate", report);
        Assert.True(result.ShieldBlocksPowerZombie.InfectionTelemetry.AverageBlockedPowerZombieInfectionsPerMatch > 0);

        Console.WriteLine(report);
    }
}
