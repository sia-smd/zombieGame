namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.Bots;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public sealed class VotingMatchCounts
{
    public int TotalVotesCast { get; init; }
    public int VotesTargetingInfected { get; init; }
    public int VoteEliminations { get; init; }
    public int ZombieVoteEliminations { get; init; }
    public int PowerZombieVoteEliminations { get; init; }
    public int StartingZombies { get; init; }
    public int StartingPowerZombies { get; init; }
    public double SumSuspicionTowardHumans { get; init; }
    public double SumSuspicionTowardZombies { get; init; }
    public double SumSuspicionTowardPowerZombies { get; init; }
    public int SuspicionSamplesTowardHumans { get; init; }
    public int SuspicionSamplesTowardZombies { get; init; }
    public int SuspicionSamplesTowardPowerZombies { get; init; }
}

public sealed class VotingTelemetry
{
    public double AverageSuspicionTowardHumans { get; set; }
    public double AverageSuspicionTowardZombies { get; set; }
    public double AverageSuspicionTowardPowerZombies { get; set; }
    public double VoteAccuracy { get; set; }
    public double ZombieEliminationRate { get; set; }
    public double PowerZombieEliminationRate { get; set; }
    public long TotalVotesCast { get; set; }
    public long TotalVoteEliminations { get; set; }
}

public sealed class SuspicionVotingComparisonResult
{
    public MatchSimulationOptions Options { get; set; } = new();
    public MatchSimulationResult RandomVoting { get; set; } = new();
    public MatchSimulationResult SuspicionVoting { get; set; } = new();
    public TimeSpan TotalElapsed { get; set; }
}

public static class SuspicionVotingComparisonFormatter
{
    public static string Format(SuspicionVotingComparisonResult result)
    {
        var lines = new List<string>
        {
            "=== Suspicion-Based Voting Experiment ===",
            $"Simulations per mode: {result.Options.MatchCount:N0}",
            $"Players: {result.Options.PlayerCount} | Seed: {result.Options.RandomSeed?.ToString() ?? "random"}",
            $"Total elapsed: {result.TotalElapsed.TotalSeconds:F2}s",
            "",
            FormatRow("Metric", "Random Voting", "Suspicion Voting"),
            FormatRow("Human Win Rate", WinRate(result.RandomVoting), WinRate(result.SuspicionVoting)),
            FormatRow("Zombie Win Rate", ZombieWinRate(result.RandomVoting), ZombieWinRate(result.SuspicionVoting)),
            FormatRow("Average Match Length", Avg(result.RandomVoting.Balance.AverageTurns), Avg(result.SuspicionVoting.Balance.AverageTurns)),
            FormatRow("Vote Accuracy", Avg(result.RandomVoting.VotingTelemetry.VoteAccuracy, "%"), Avg(result.SuspicionVoting.VotingTelemetry.VoteAccuracy, "%")),
            FormatRow("Zombie Elimination Rate", Avg(result.RandomVoting.VotingTelemetry.ZombieEliminationRate, "%"), Avg(result.SuspicionVoting.VotingTelemetry.ZombieEliminationRate, "%")),
            FormatRow("PowerZombie Elimination Rate", Avg(result.RandomVoting.VotingTelemetry.PowerZombieEliminationRate, "%"), Avg(result.SuspicionVoting.VotingTelemetry.PowerZombieEliminationRate, "%")),
            FormatRow("Avg Suspicion (Human targets)", Avg(result.RandomVoting.VotingTelemetry.AverageSuspicionTowardHumans), Avg(result.SuspicionVoting.VotingTelemetry.AverageSuspicionTowardHumans)),
            FormatRow("Avg Suspicion (Zombie targets)", Avg(result.RandomVoting.VotingTelemetry.AverageSuspicionTowardZombies), Avg(result.SuspicionVoting.VotingTelemetry.AverageSuspicionTowardZombies)),
            FormatRow("Avg Suspicion (PowerZombie targets)", Avg(result.RandomVoting.VotingTelemetry.AverageSuspicionTowardPowerZombies), Avg(result.SuspicionVoting.VotingTelemetry.AverageSuspicionTowardPowerZombies)),
            FormatRow("Human Passes/Match", Avg(result.RandomVoting.PassTelemetry.Humans.AveragePassesPerMatch), Avg(result.SuspicionVoting.PassTelemetry.Humans.AveragePassesPerMatch)),
            FormatRow("Zombie Passes/Match", Avg(result.RandomVoting.PassTelemetry.Zombies.AveragePassesPerMatch), Avg(result.SuspicionVoting.PassTelemetry.Zombies.AveragePassesPerMatch)),
            FormatRow("PowerZombie Passes/Match", Avg(result.RandomVoting.PassTelemetry.PowerZombies.AveragePassesPerMatch), Avg(result.SuspicionVoting.PassTelemetry.PowerZombies.AveragePassesPerMatch)),
            "",
            "=== Delta (Suspicion - Random) ===",
            $"Human Win Rate: {Delta(result.RandomVoting.Balance.HumanWinRate, result.SuspicionVoting.Balance.HumanWinRate):+#.##;-#.##;0.00} pp",
            $"Vote Accuracy: {Delta(result.RandomVoting.VotingTelemetry.VoteAccuracy, result.SuspicionVoting.VotingTelemetry.VoteAccuracy):+#.##;-#.##;0.00} pp",
            $"Zombie Elimination Rate: {Delta(result.RandomVoting.VotingTelemetry.ZombieEliminationRate, result.SuspicionVoting.VotingTelemetry.ZombieEliminationRate):+#.##;-#.##;0.00} pp",
            $"PowerZombie Elimination Rate: {Delta(result.RandomVoting.VotingTelemetry.PowerZombieEliminationRate, result.SuspicionVoting.VotingTelemetry.PowerZombieEliminationRate):+#.##;-#.##;0.00} pp"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatRow(string label, string random, string suspicion) =>
        $"{label,-34} | {random,14} | {suspicion,14}";

    private static string WinRate(MatchSimulationResult r) => $"{r.Balance.HumanWinRate:F2}%";
    private static string ZombieWinRate(MatchSimulationResult r) => $"{r.Balance.ZombieWinRate:F2}%";
    private static string Avg(double value, string suffix = "") => suffix == "%" ? $"{value:F2}%" : $"{value:F2}";
    private static double Delta(double a, double b) => Math.Round(b - a, 2);
}

public static class VotingTelemetryRecorder
{
    public static void RecordVote(
        MutableVotingMatchCounts counts,
        GameSessionState state,
        Guid voterId,
        Guid targetId)
    {
        counts.TotalVotesCast++;
        var target = state.GetPlayer(targetId);
        if (target?.IsInfectedTeam == true)
            counts.VotesTargetingInfected++;

        foreach (var candidate in state.AlivePlayers.Where(p => p.UserId != voterId))
        {
            var suspicion = SuspicionScoring.GetSuspicion(state, voterId, candidate.UserId);
            switch (candidate.Role)
            {
                case PlayerRole.Human:
                    counts.SumSuspicionTowardHumans += suspicion;
                    counts.SuspicionSamplesTowardHumans++;
                    break;
                case PlayerRole.Zombie:
                    counts.SumSuspicionTowardZombies += suspicion;
                    counts.SuspicionSamplesTowardZombies++;
                    break;
                case PlayerRole.PowerZombie:
                    counts.SumSuspicionTowardPowerZombies += suspicion;
                    counts.SuspicionSamplesTowardPowerZombies++;
                    break;
            }
        }
    }

    public static void RecordElimination(MutableVotingMatchCounts counts, GamePlayerState eliminated)
    {
        counts.VoteEliminations++;
        switch (eliminated.Role)
        {
            case PlayerRole.Zombie:
                counts.ZombieVoteEliminations++;
                break;
            case PlayerRole.PowerZombie:
                counts.PowerZombieVoteEliminations++;
                break;
        }
    }

    public static VotingMatchCounts ToImmutable(MutableVotingMatchCounts counts) =>
        new()
        {
            TotalVotesCast = counts.TotalVotesCast,
            VotesTargetingInfected = counts.VotesTargetingInfected,
            VoteEliminations = counts.VoteEliminations,
            ZombieVoteEliminations = counts.ZombieVoteEliminations,
            PowerZombieVoteEliminations = counts.PowerZombieVoteEliminations,
            StartingZombies = counts.StartingZombies,
            StartingPowerZombies = counts.StartingPowerZombies,
            SumSuspicionTowardHumans = counts.SumSuspicionTowardHumans,
            SumSuspicionTowardZombies = counts.SumSuspicionTowardZombies,
            SumSuspicionTowardPowerZombies = counts.SumSuspicionTowardPowerZombies,
            SuspicionSamplesTowardHumans = counts.SuspicionSamplesTowardHumans,
            SuspicionSamplesTowardZombies = counts.SuspicionSamplesTowardZombies,
            SuspicionSamplesTowardPowerZombies = counts.SuspicionSamplesTowardPowerZombies
        };
}

public sealed class MutableVotingMatchCounts
{
    public int TotalVotesCast { get; set; }
    public int VotesTargetingInfected { get; set; }
    public int VoteEliminations { get; set; }
    public int ZombieVoteEliminations { get; set; }
    public int PowerZombieVoteEliminations { get; set; }
    public int StartingZombies { get; set; }
    public int StartingPowerZombies { get; set; }
    public double SumSuspicionTowardHumans { get; set; }
    public double SumSuspicionTowardZombies { get; set; }
    public double SumSuspicionTowardPowerZombies { get; set; }
    public int SuspicionSamplesTowardHumans { get; set; }
    public int SuspicionSamplesTowardZombies { get; set; }
    public int SuspicionSamplesTowardPowerZombies { get; set; }
}
