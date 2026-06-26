namespace ZombieGame.Application.Simulation;

public sealed class PassPenaltyComparisonResult
{
    public MatchSimulationOptions Options { get; set; } = new();
    public MatchSimulationResult NoPenalty { get; set; } = new();
    public MatchSimulationResult PassPenaltyMode { get; set; } = new();
    public TimeSpan TotalElapsed { get; set; }
}

public static class PassPenaltyComparisonFormatter
{
    public static string Format(PassPenaltyComparisonResult result)
    {
        var lines = new List<string>
        {
            "=== Pass Penalty Mode Experiment ===",
            $"Simulations per mode: {result.Options.MatchCount:N0}",
            $"Players: {result.Options.PlayerCount} | Seed: {result.Options.RandomSeed?.ToString() ?? "random"}",
            $"Total elapsed: {result.TotalElapsed.TotalSeconds:F2}s",
            "",
            FormatModeRow("Metric", "No Penalty", "PassPenaltyMode"),
            FormatModeRow("Human Win Rate", FormatWinRate(result.NoPenalty), FormatWinRate(result.PassPenaltyMode)),
            FormatModeRow("Zombie Win Rate", FormatZombieWinRate(result.NoPenalty), FormatZombieWinRate(result.PassPenaltyMode)),
            FormatModeRow("Stalemate Rate", FormatStalemate(result.NoPenalty), FormatStalemate(result.PassPenaltyMode)),
            FormatModeRow("Avg Turns (Days)", FormatAvg(result.NoPenalty.Balance.AverageTurns), FormatAvg(result.PassPenaltyMode.Balance.AverageTurns)),
            FormatModeRow("Avg Card Plays/Match", FormatAvg(result.NoPenalty.Balance.AverageCardPlaysPerMatch), FormatAvg(result.PassPenaltyMode.Balance.AverageCardPlaysPerMatch)),
            FormatModeRow("Avg Vote Eliminations", FormatAvg(result.NoPenalty.Balance.AverageEliminationsByVote), FormatAvg(result.PassPenaltyMode.Balance.AverageEliminationsByVote)),
            FormatModeRow("Avg Friendly Fire", FormatAvg(result.NoPenalty.Balance.AverageFriendlyFirePerMatch), FormatAvg(result.PassPenaltyMode.Balance.AverageFriendlyFirePerMatch)),
            FormatModeRow("Human Passes/Match", FormatAvg(result.NoPenalty.PassTelemetry.Humans.AveragePassesPerMatch), FormatAvg(result.PassPenaltyMode.PassTelemetry.Humans.AveragePassesPerMatch)),
            FormatModeRow("Zombie Passes/Match", FormatAvg(result.NoPenalty.PassTelemetry.Zombies.AveragePassesPerMatch), FormatAvg(result.PassPenaltyMode.PassTelemetry.Zombies.AveragePassesPerMatch)),
            FormatModeRow("PowerZombie Passes/Match", FormatAvg(result.NoPenalty.PassTelemetry.PowerZombies.AveragePassesPerMatch), FormatAvg(result.PassPenaltyMode.PassTelemetry.PowerZombies.AveragePassesPerMatch)),
            "",
            "=== Delta (PassPenaltyMode - No Penalty) ===",
            $"Human Win Rate: {Delta(result.NoPenalty.Balance.HumanWinRate, result.PassPenaltyMode.Balance.HumanWinRate):+#.##;-#.##;0.00} pp",
            $"Zombie Win Rate: {Delta(result.NoPenalty.Balance.ZombieWinRate, result.PassPenaltyMode.Balance.ZombieWinRate):+#.##;-#.##;0.00} pp",
            $"Avg Turns: {Delta(result.NoPenalty.Balance.AverageTurns, result.PassPenaltyMode.Balance.AverageTurns):+#.##;-#.##;0.00}",
            $"Avg Card Plays: {Delta(result.NoPenalty.Balance.AverageCardPlaysPerMatch, result.PassPenaltyMode.Balance.AverageCardPlaysPerMatch):+#.##;-#.##;0.00}",
            $"Human Passes/Match: {Delta(result.NoPenalty.PassTelemetry.Humans.AveragePassesPerMatch, result.PassPenaltyMode.PassTelemetry.Humans.AveragePassesPerMatch):+#.##;-#.##;0.00}",
            $"Zombie Passes/Match: {Delta(result.NoPenalty.PassTelemetry.Zombies.AveragePassesPerMatch, result.PassPenaltyMode.PassTelemetry.Zombies.AveragePassesPerMatch):+#.##;-#.##;0.00}",
            $"PowerZombie Passes/Match: {Delta(result.NoPenalty.PassTelemetry.PowerZombies.AveragePassesPerMatch, result.PassPenaltyMode.PassTelemetry.PowerZombies.AveragePassesPerMatch):+#.##;-#.##;0.00}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatModeRow(string label, string noPenalty, string passPenalty) =>
        $"{label,-28} | {noPenalty,14} | {passPenalty,14}";

    private static string FormatWinRate(MatchSimulationResult result) =>
        $"{result.Balance.HumanWinRate:F2}%";

    private static string FormatZombieWinRate(MatchSimulationResult result) =>
        $"{result.Balance.ZombieWinRate:F2}%";

    private static string FormatStalemate(MatchSimulationResult result) =>
        $"{result.Balance.StalemateRate:F2}%";

    private static string FormatAvg(double value) => $"{value:F2}";

    private static double Delta(double baseline, double variant) =>
        Math.Round(variant - baseline, 2);
}
