namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.Simulation;

public class SuspicionVotingComparisonTests
{
    [Fact]
    public async Task RunSuspicionVotingComparison_ProducesBothModes()
    {
        var service = new MatchSimulationService();
        var result = await service.RunSuspicionVotingComparisonAsync(new MatchSimulationOptions
        {
            MatchCount = 100,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = 4
        });

        Assert.Equal(100, result.RandomVoting.Balance.TotalMatches);
        Assert.Equal(100, result.SuspicionVoting.Balance.TotalMatches);
        Assert.True(result.SuspicionVoting.VotingTelemetry.VoteAccuracy >= 0);
    }

    [Fact]
    public async Task RunSuspicionVotingComparison_10k_GeneratesReport()
    {
        var service = new MatchSimulationService();
        var result = await service.RunSuspicionVotingComparisonAsync(new MatchSimulationOptions
        {
            MatchCount = 10_000,
            PlayerCount = 8,
            RandomSeed = 42,
            MaxParallelism = Environment.ProcessorCount
        });

        var report = SuspicionVotingComparisonFormatter.Format(result);

        Assert.Contains("Random Voting", report);
        Assert.Contains("Suspicion Voting", report);
        Assert.Contains("Vote Accuracy", report);
        Assert.Contains("Zombie Elimination Rate", report);
        Assert.Contains("Human Passes/Match", report);

        Console.WriteLine(report);
    }
}
