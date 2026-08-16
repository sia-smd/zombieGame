namespace ZombieGame.Application.Tests.Simulation;

using ZombieGame.Application.Simulation;
using Xunit.Abstractions;

public class DetailedReplayRunnerTests
{
    private readonly ITestOutputHelper _output;

    public DetailedReplayRunnerTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Run12PlayerDetailedReplay_PrintsFullReport()
    {
        var service = new MatchSimulationService();
        var replay = await service.RunDetailedReplayAsync(new MatchSimulationOptions
        {
            PlayerCount = 12,
            MaxTurnsPerMatch = 50,
            RandomSeed = 42,
            UseSuspicionBasedVoting = true,
            MaxPassActionsPerDay = 0
        });

        var text = MatchReplayTextFormatter.Format(replay);
        _output.WriteLine(text);

        Assert.Equal(12, replay.PlayerCount);
        Assert.NotEmpty(replay.Days);
        Assert.True(replay.Winner.HasValue || replay.IsStalemate);

        var day1 = replay.Days[0];
        Assert.Equal(12, day1.StartingHands.Count);
        Assert.All(day1.StartingHands, h =>
        {
            Assert.False(string.IsNullOrWhiteSpace(h.RoleCard));
            Assert.False(string.IsNullOrWhiteSpace(h.InventorySlot1));
            Assert.False(string.IsNullOrWhiteSpace(h.InventorySlot2));
        });
        Assert.Contains("Hands at day start", text);
        Assert.Contains("Slot 1:", text);
    }
}
