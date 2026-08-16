namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.Simulation;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;
using Xunit.Abstractions;

[Trait("Category", "Benchmark")]
public class TwelvePlayerTenKBalanceTests
{
    public const int DefaultMatchCount = 10_000;
    public const int DefaultPlayerCount = 12;
    public const int DefaultRandomSeed = 42;

    private readonly ITestOutputHelper _output;

    public TwelvePlayerTenKBalanceTests(ITestOutputHelper output) => _output = output;

    public static MatchSimulationOptions CreateOptions(
        int matchCount = DefaultMatchCount,
        int? randomSeed = DefaultRandomSeed) =>
        new()
        {
            MatchCount = matchCount,
            PlayerCount = DefaultPlayerCount,
            MaxTurnsPerMatch = 50,
            RandomSeed = randomSeed,
            MaxParallelism = Environment.ProcessorCount,
            UseSuspicionBasedVoting = true,
            MaxPassActionsPerDay = 0,
            Scenario = BalanceScenarioType.Standard
        };

    [Fact]
    public async Task TwelvePlayer_10k_Matches_ProducesBalanceReport()
    {
        var options = CreateOptions();
        var service = new MatchSimulationService();
        var result = await service.RunAsync(options);

        Assert.Equal(DefaultMatchCount, result.Balance.TotalMatches);
        Assert.Equal(DefaultMatchCount,
            result.Balance.HumanWins + result.Balance.ZombieWins + result.Balance.Stalemates);

        await AssertStartingRoleCountsAsync(options);

        var report = SimulationBalanceReportFormatter.FormatComplete(result);
        _output.WriteLine(report);

        var reportPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            $"balance-12players-{DefaultMatchCount}-seed{DefaultRandomSeed}.txt"));
        await ReportFileWriter.WriteUtf8Async(reportPath, report);
        _output.WriteLine($"Saved: {reportPath}");

        AssertBalanceSanity(result);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    public async Task TwelvePlayer_Sample_Matches_ProduceStableMetrics(int matchCount)
    {
        var service = new MatchSimulationService();
        var options = CreateOptions(matchCount, randomSeed: 7);
        options.MaxParallelism = 1;

        var result = await service.RunAsync(options);

        Assert.Equal(matchCount, result.Balance.TotalMatches);
        Assert.True(result.Balance.AverageCardPlaysPerMatch > 0);
        Assert.True(result.PassTelemetry.HumanActions.PassRatePercent > 0);
    }

    [Fact]
    public void RoleAssignment_TwelvePlayers_AssignsTwoZombiesAndOnePowerZombie()
    {
        var players = Enumerable.Range(0, 12)
            .Select(_ => (Guid.NewGuid(), PlayerRole.Human, true))
            .ToArray();
        var state = GameTestBuilder.CreateSession(players);

        new RoleAssignmentService().AssignRoles(state, 12);

        Assert.Equal(10, state.Players.Count(p => p.Role == PlayerRole.Human));
        Assert.Equal(1, state.Players.Count(p => p.Role == PlayerRole.Zombie));
        Assert.Equal(1, state.Players.Count(p => p.Role == PlayerRole.PowerZombie));
    }

    private static async Task AssertStartingRoleCountsAsync(MatchSimulationOptions options)
    {
        var simulator = new MatchSimulator(options);
        var outcome = await simulator.RunSingleMatchAsync(options, new Random(options.RandomSeed!.Value));

        Assert.Equal(10, outcome.HumanPlayersAtStart);
        Assert.Equal(1, outcome.ZombiePlayersAtStart);
        Assert.Equal(1, outcome.PowerZombiePlayersAtStart);
    }

    private static void AssertBalanceSanity(MatchSimulationResult result)
    {
        var b = result.Balance;
        var p = result.PassTelemetry;

        Assert.InRange(b.HumanWinRate + b.ZombieWinRate + b.StalemateRate, 99.0, 101.0);
        Assert.True(b.AverageTurns > 0);
        Assert.True(b.MinTurns > 0);
        Assert.True(b.MaxTurns >= b.MinTurns);
        Assert.True(b.MaxTurns <= result.Options.MaxTurnsPerMatch + 1);
        Assert.True(b.AverageCardPlaysPerMatch > 0);

        Assert.InRange(p.HumanActions.PassRatePercent, 0, 100);
        Assert.InRange(p.ZombieActions.PassRatePercent, 0, 100);
        Assert.InRange(p.PowerZombieActions.PassRatePercent, 0, 100);

        Assert.NotEmpty(b.DayEventOccurrences);
        Assert.NotEmpty(b.CardPlaysByEffect);
        Assert.True(b.CardPlaysByEffect.GetValueOrDefault("shoot") > 0);
        Assert.True(b.CardPlaysByEffect.GetValueOrDefault("heal") > 0);
    }
}
