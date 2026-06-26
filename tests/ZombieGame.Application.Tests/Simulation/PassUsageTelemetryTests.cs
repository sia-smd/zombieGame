namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.Simulation;
using ZombieGame.Domain.Enums;

public class PassUsageTelemetryTests
{
    [Fact]
    public void PearsonCorrelation_PerfectPositive()
    {
        var xs = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var ys = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        var r = BalanceStatisticsAggregator.PearsonCorrelation(xs, ys);

        Assert.Equal(1.0, r);
    }

    [Fact]
    public void PearsonCorrelation_PerfectNegative()
    {
        var xs = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var ys = new[] { 5.0, 4.0, 3.0, 2.0, 1.0 };

        var r = BalanceStatisticsAggregator.PearsonCorrelation(xs, ys);

        Assert.Equal(-1.0, r);
    }

    [Fact]
    public async Task RunAsync_CollectsPassUsageTelemetry()
    {
        var service = new MatchSimulationService();
        var result = await service.RunAsync(new MatchSimulationOptions
        {
            MatchCount = 200,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = 4
        });

        Assert.True(result.PassTelemetry.Humans.TotalPasses > 0);
        Assert.True(result.PassTelemetry.Zombies.TotalPasses >= 0);
        Assert.True(result.PassTelemetry.PowerZombies.TotalPasses >= 0);
        Assert.True(result.PassTelemetry.TotalDays > 0);
        Assert.True(result.PassTelemetry.Humans.AveragePassesPerMatch >= 0);
        Assert.True(result.PassTelemetry.Humans.AveragePassesPerDay >= 0);
        Assert.True(result.PassTelemetry.Humans.AveragePassesPerPlayer >= 0);
        Assert.InRange(result.PassTelemetry.Correlation.TotalPassCountVsHumanWin, -1, 1);
    }

    [Fact]
    public async Task RunAsync_10k_PassReport_IsGenerated()
    {
        var service = new MatchSimulationService();
        var result = await service.RunAsync(new MatchSimulationOptions
        {
            MatchCount = 10_000,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = Environment.ProcessorCount
        });

        var report = SimulationReportFormatter.FormatPassUsageReport(result);

        Assert.Equal(10_000, result.Balance.TotalMatches);
        Assert.Contains("Pass Usage By Role", report);
        Assert.Contains("Humans:", report);
        Assert.Contains("Zombies:", report);
        Assert.Contains("PowerZombies:", report);
        Assert.Contains("Pass Usage vs Win Rate", report);

        Console.WriteLine(report);
    }
}
