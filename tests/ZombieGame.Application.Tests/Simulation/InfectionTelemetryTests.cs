namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Simulation;

public class InfectionTelemetryTests
{
    [Fact]
    public async Task RunAsync_CollectsInfectionTelemetry()
    {
        var service = new MatchSimulationService();
        var result = await service.RunAsync(new MatchSimulationOptions
        {
            MatchCount = 200,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = 4
        });

        var i = result.InfectionTelemetry;
        Assert.True(i.TotalSuccessfulZombieInfections > 0);
        Assert.True(i.TotalSuccessfulPowerZombieInfections > 0);
        Assert.True(i.AverageSuccessfulInfectionsPerMatch > 0);
        Assert.True(i.TotalDisabledShotgunsAfterInfection >= 0);
        Assert.True(i.TotalDisabledHealsAfterInfection >= 0);
        Assert.True(i.InfectionSuccessRate >= 0 && i.InfectionSuccessRate <= 100);
        Assert.True(i.PowerZombieInfectionSuccessRate >= 0 && i.PowerZombieInfectionSuccessRate <= 100);
        Assert.True(i.HealEffectivenessRate >= 0 && i.HealEffectivenessRate <= 100);
        Assert.Equal(
            i.TotalHumansConvertedToZombies,
            i.TotalSuccessfulZombieInfections + i.TotalSuccessfulPowerZombieInfections);
    }

    [Fact]
    public async Task RunAsync_10k_GeneratesInfectionBalanceReport()
    {
        var service = new MatchSimulationService();
        var result = await service.RunAsync(new MatchSimulationOptions
        {
            MatchCount = 10_000,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = Environment.ProcessorCount
        });

        var report = InfectionBalanceReportFormatter.Format(result);

        Assert.Contains("Infection Balance Report", report);
        Assert.Contains("Infection Success Rate", report);
        Assert.Contains("PowerZombie Infection Success Rate", report);
        Assert.Contains("Heal Effectiveness Rate", report);
        Assert.Contains("Infection Transformation", report);
        Assert.Contains("Human Passes/Match", report);

        Console.WriteLine(report);
    }
}
