namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.Simulation;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using Xunit.Abstractions;

[Trait("Category", "Benchmark")]
public class BalanceScenarioTests
{
    public const int ScenarioMatchCount = 1_000;
    public const int DefaultPlayerCount = 12;
    public const int DefaultRandomSeed = 42;

    private readonly ITestOutputHelper _output;

    public BalanceScenarioTests(ITestOutputHelper output) => _output = output;

    public static MatchSimulationOptions CreateScenarioOptions(
        BalanceScenarioType scenario,
        int matchCount = ScenarioMatchCount,
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
            Scenario = scenario
        };

    [Theory]
    [InlineData(BalanceScenarioType.Standard)]
    [InlineData(BalanceScenarioType.StrongHumans)]
    [InlineData(BalanceScenarioType.StrongZombies)]
    [InlineData(BalanceScenarioType.NoPowerZombie)]
    public async Task Scenario_RunsMatches_AndProducesReport(BalanceScenarioType scenario)
    {
        var options = CreateScenarioOptions(scenario);
        var service = new MatchSimulationService();
        var result = await service.RunAsync(options);

        Assert.Equal(ScenarioMatchCount, result.Balance.TotalMatches);
        AssertStartingRoles(result, scenario);
        AssertScenarioSanity(result, scenario);

        var report = SimulationBalanceReportFormatter.FormatComplete(result);
        _output.WriteLine(report);

        var fileName = $"balance-scenario-{scenario}-seed{DefaultRandomSeed}.txt";
        var reportPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", fileName));
        await ReportFileWriter.WriteUtf8Async(reportPath, report);
        _output.WriteLine($"Saved: {reportPath}");
    }

    [Fact]
    public async Task ScenarioC_NoPowerZombie_ComparedToStandard_ShowsMeasurableDifference()
    {
        var service = new MatchSimulationService();
        var standard = await service.RunAsync(CreateScenarioOptions(BalanceScenarioType.Standard));
        var noPower = await service.RunAsync(CreateScenarioOptions(BalanceScenarioType.NoPowerZombie, randomSeed: 43));

        var comparison = SimulationBalanceReportFormatter.FormatScenarioComparison(
            standard,
            noPower,
            BalanceScenarioProfiles.GetDisplayName(BalanceScenarioType.NoPowerZombie));

        _output.WriteLine(comparison);

        var reportPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "balance-scenario-comparison-no-power.txt"));
        await ReportFileWriter.WriteUtf8Async(reportPath, comparison);
        _output.WriteLine($"Saved: {reportPath}");

        Assert.True(
            Math.Abs(standard.Balance.HumanWinRate - noPower.Balance.HumanWinRate) > 0.1 ||
            Math.Abs(standard.Balance.AverageTurns - noPower.Balance.AverageTurns) > 0.1,
            "Expected measurable difference between Standard and NoPowerZombie scenarios.");
        Assert.Equal(0, noPower.Balance.WinRateWhenRolePresentAtStart.GetValueOrDefault("PowerZombie"));
    }

    [Fact]
    public void PowerZombiePersonality_GetsAggressionAndTalkModifiers()
    {
        var personality = new BotPersonality
        {
            Aggression = 40,
            RiskTolerance = 30,
            TalkManipulation = 20,
            CommunicationStyle = CommunicationStyle.Quiet
        };

        BotPersonalityFactory.ApplyPowerZombieModifiers(personality);

        Assert.Equal(60, personality.Aggression);
        Assert.Equal(50, personality.RiskTolerance);
        Assert.Equal(30, personality.TalkManipulation);
        Assert.Equal(CommunicationStyle.Balanced, personality.CommunicationStyle);
    }

    [Fact]
    public void InitializeBots_PowerZombie_HasHigherAggressionThanTypicalZombie()
    {
        var powerId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var humanId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true),
            (powerId, PlayerRole.PowerZombie, true));
        state.Players.ForEach(p => p.IsBot = true);

        BotObservationRecorder.InitializeBots(state, new Random(99));

        var power = state.BotCognition[powerId].Personality;
        var zombie = state.BotCognition[zombieId].Personality;

        Assert.True(power.Aggression >= zombie.Aggression);
        Assert.True(power.TalkManipulation >= zombie.TalkManipulation);
        Assert.True(power.RiskTolerance >= zombie.RiskTolerance);
    }

    private static void AssertStartingRoles(MatchSimulationResult result, BalanceScenarioType scenario)
    {
        var composition = BalanceScenarioProfiles.GetComposition(scenario, result.Options.PlayerCount);
        var simulator = new MatchSimulator(result.Options);
        var outcome = simulator.RunSingleMatchAsync(result.Options, new Random(result.Options.RandomSeed!.Value))
            .GetAwaiter().GetResult();

        Assert.Equal(composition.HumanCount(result.Options.PlayerCount), outcome.HumanPlayersAtStart);
        Assert.Equal(composition.ZombieCount, outcome.ZombiePlayersAtStart);
        Assert.Equal(composition.PowerZombieCount, outcome.PowerZombiePlayersAtStart);
    }

    private static void AssertScenarioSanity(MatchSimulationResult result, BalanceScenarioType scenario)
    {
        var b = result.Balance;
        var p = result.PassTelemetry;

        Assert.InRange(b.HumanWinRate + b.ZombieWinRate + b.StalemateRate, 99.0, 101.0);
        Assert.True(b.AverageTurns > 0);
        Assert.True(b.MinTurns > 0);
        Assert.True(b.AverageCardPlaysPerMatch > 0);
        Assert.True(b.CardPlaysByEffect.GetValueOrDefault("shoot") > 0);
        Assert.True(b.CardPlaysByEffect.GetValueOrDefault("heal") > 0);

        switch (scenario)
        {
            case BalanceScenarioType.StrongHumans:
                Assert.True(b.HumanWins > 0, "Strong humans: expected at least some human wins.");
                Assert.True(b.ZombieWins > 0, "Strong humans: zombies should still win sometimes.");
                break;
            case BalanceScenarioType.StrongZombies:
                Assert.True(b.ZombieWins > 0, "Strong zombies: expected zombie wins.");
                Assert.True(
                    b.ZombieWinRate >= 50,
                    $"Strong zombies should favor zombies (H={b.HumanWinRate:F1}%, Z={b.ZombieWinRate:F1}%).");
                break;
            case BalanceScenarioType.NoPowerZombie:
                Assert.False(b.WinRateWhenRolePresentAtStart.ContainsKey("PowerZombie"));
                break;
            case BalanceScenarioType.Standard:
                Assert.True(b.HumanWins > 0 && b.ZombieWins > 0, "Standard: both teams should win in 1000 games.");
                break;
        }
    }
}
