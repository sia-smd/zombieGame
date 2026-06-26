namespace ZombieGame.Application.Simulation;

public sealed class MaxPassLimitComparisonResult
{
    public MatchSimulationOptions Options { get; set; } = new();
    public MatchSimulationResult UnlimitedPass { get; set; } = new();
    public MatchSimulationResult MaxOnePassPerDay { get; set; } = new();
    public TimeSpan TotalElapsed { get; set; }
}

public static class MaxPassLimitComparisonFormatter
{
    public static string Format(MaxPassLimitComparisonResult result)
    {
        var lines = new List<string>
        {
            "=== MaxPassActionsPerDay Experiment ===",
            $"Simulations per mode: {result.Options.MatchCount:N0}",
            $"Players: {result.Options.PlayerCount} | Seed: {result.Options.RandomSeed?.ToString() ?? "random"}",
            $"Total elapsed: {result.TotalElapsed.TotalSeconds:F2}s",
            "",
            FormatModeRow("Metric", "Unlimited Pass", "MaxPassPerDay=1"),
            FormatModeRow("Human Win Rate", FormatWinRate(result.UnlimitedPass), FormatWinRate(result.MaxOnePassPerDay)),
            FormatModeRow("Zombie Win Rate", FormatZombieWinRate(result.UnlimitedPass), FormatZombieWinRate(result.MaxOnePassPerDay)),
            FormatModeRow("Average Match Length", FormatAvg(result.UnlimitedPass.Balance.AverageTurns), FormatAvg(result.MaxOnePassPerDay.Balance.AverageTurns)),
            FormatModeRow("Average Pass Usage", FormatAvg(TotalPassesPerMatch(result.UnlimitedPass)), FormatAvg(TotalPassesPerMatch(result.MaxOnePassPerDay))),
            FormatModeRow("Average Vote Eliminations", FormatAvg(result.UnlimitedPass.Balance.AverageEliminationsByVote), FormatAvg(result.MaxOnePassPerDay.Balance.AverageEliminationsByVote)),
            FormatModeRow("Human Passes/Match", FormatAvg(result.UnlimitedPass.PassTelemetry.Humans.AveragePassesPerMatch), FormatAvg(result.MaxOnePassPerDay.PassTelemetry.Humans.AveragePassesPerMatch)),
            FormatModeRow("Zombie Passes/Match", FormatAvg(result.UnlimitedPass.PassTelemetry.Zombies.AveragePassesPerMatch), FormatAvg(result.MaxOnePassPerDay.PassTelemetry.Zombies.AveragePassesPerMatch)),
            FormatModeRow("PowerZombie Passes/Match", FormatAvg(result.UnlimitedPass.PassTelemetry.PowerZombies.AveragePassesPerMatch), FormatAvg(result.MaxOnePassPerDay.PassTelemetry.PowerZombies.AveragePassesPerMatch)),
            "",
            "=== Delta (MaxPassPerDay=1 - Unlimited) ===",
            $"Human Win Rate: {Delta(result.UnlimitedPass.Balance.HumanWinRate, result.MaxOnePassPerDay.Balance.HumanWinRate):+#.##;-#.##;0.00} pp",
            $"Zombie Win Rate: {Delta(result.UnlimitedPass.Balance.ZombieWinRate, result.MaxOnePassPerDay.Balance.ZombieWinRate):+#.##;-#.##;0.00} pp",
            $"Average Match Length: {Delta(result.UnlimitedPass.Balance.AverageTurns, result.MaxOnePassPerDay.Balance.AverageTurns):+#.##;-#.##;0.00}",
            $"Average Pass Usage: {Delta(TotalPassesPerMatch(result.UnlimitedPass), TotalPassesPerMatch(result.MaxOnePassPerDay)):+#.##;-#.##;0.00}",
            $"Average Vote Eliminations: {Delta(result.UnlimitedPass.Balance.AverageEliminationsByVote, result.MaxOnePassPerDay.Balance.AverageEliminationsByVote):+#.##;-#.##;0.00}",
            $"Human Passes/Match: {Delta(result.UnlimitedPass.PassTelemetry.Humans.AveragePassesPerMatch, result.MaxOnePassPerDay.PassTelemetry.Humans.AveragePassesPerMatch):+#.##;-#.##;0.00}",
            $"Zombie Passes/Match: {Delta(result.UnlimitedPass.PassTelemetry.Zombies.AveragePassesPerMatch, result.MaxOnePassPerDay.PassTelemetry.Zombies.AveragePassesPerMatch):+#.##;-#.##;0.00}",
            $"PowerZombie Passes/Match: {Delta(result.UnlimitedPass.PassTelemetry.PowerZombies.AveragePassesPerMatch, result.MaxOnePassPerDay.PassTelemetry.PowerZombies.AveragePassesPerMatch):+#.##;-#.##;0.00}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static double TotalPassesPerMatch(MatchSimulationResult result) =>
        result.PassTelemetry.Humans.AveragePassesPerMatch +
        result.PassTelemetry.Zombies.AveragePassesPerMatch +
        result.PassTelemetry.PowerZombies.AveragePassesPerMatch;

    private static string FormatModeRow(string label, string unlimited, string limited) =>
        $"{label,-28} | {unlimited,14} | {limited,14}";

    private static string FormatWinRate(MatchSimulationResult result) =>
        $"{result.Balance.HumanWinRate:F2}%";

    private static string FormatZombieWinRate(MatchSimulationResult result) =>
        $"{result.Balance.ZombieWinRate:F2}%";

    private static string FormatAvg(double value) => $"{value:F2}";

    private static double Delta(double baseline, double variant) =>
        Math.Round(variant - baseline, 2);
}
