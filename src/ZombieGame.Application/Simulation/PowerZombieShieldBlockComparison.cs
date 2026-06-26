namespace ZombieGame.Application.Simulation;

public sealed class PowerZombieShieldBlockComparisonResult
{
    public MatchSimulationOptions Options { get; set; } = new();
    public MatchSimulationResult CurrentRules { get; set; } = new();
    public MatchSimulationResult ShieldBlocksPowerZombie { get; set; } = new();
    public TimeSpan TotalElapsed { get; set; }
}

public static class PowerZombieShieldBlockComparisonFormatter
{
    public static string Format(PowerZombieShieldBlockComparisonResult result)
    {
        var lines = new List<string>
        {
            "=== PowerZombie Shield Block Experiment ===",
            $"Simulations per mode: {result.Options.MatchCount:N0}",
            $"Players: {result.Options.PlayerCount} | Seed: {result.Options.RandomSeed?.ToString() ?? "random"}",
            $"Total elapsed: {result.TotalElapsed.TotalSeconds:F2}s",
            "",
            FormatRow("Metric", "Current Rules", "Shield Blocks PowerZombie"),
            FormatRow("Human Win Rate", WinRate(result.CurrentRules), WinRate(result.ShieldBlocksPowerZombie)),
            FormatRow("Zombie Win Rate", ZombieWinRate(result.CurrentRules), ZombieWinRate(result.ShieldBlocksPowerZombie)),
            FormatRow("PowerZombie Kill Rate", KillRate(result.CurrentRules), KillRate(result.ShieldBlocksPowerZombie)),
            FormatRow("Average Match Length", Avg(result.CurrentRules.Balance.AverageTurns), Avg(result.ShieldBlocksPowerZombie.Balance.AverageTurns)),
            FormatRow("Avg Successful PowerZombie Infections", Avg(result.CurrentRules.InfectionTelemetry.AverageSuccessfulPowerZombieInfectionsPerMatch), Avg(result.ShieldBlocksPowerZombie.InfectionTelemetry.AverageSuccessfulPowerZombieInfectionsPerMatch)),
            FormatRow("Avg Blocked PowerZombie Infections", Avg(result.CurrentRules.InfectionTelemetry.AverageBlockedPowerZombieInfectionsPerMatch), Avg(result.ShieldBlocksPowerZombie.InfectionTelemetry.AverageBlockedPowerZombieInfectionsPerMatch)),
            FormatRow("PowerZombie Infection Success Rate", Rate(result.CurrentRules.InfectionTelemetry.PowerZombieInfectionSuccessRate), Rate(result.ShieldBlocksPowerZombie.InfectionTelemetry.PowerZombieInfectionSuccessRate)),
            "",
            "=== Delta (Shield Blocks - Current) ===",
            $"Human Win Rate: {Delta(result.CurrentRules.Balance.HumanWinRate, result.ShieldBlocksPowerZombie.Balance.HumanWinRate):+#.##;-#.##;0.00} pp",
            $"Zombie Win Rate: {Delta(result.CurrentRules.Balance.ZombieWinRate, result.ShieldBlocksPowerZombie.Balance.ZombieWinRate):+#.##;-#.##;0.00} pp",
            $"PowerZombie Kill Rate: {Delta(result.CurrentRules.InfectionTelemetry.PowerZombieKillRate, result.ShieldBlocksPowerZombie.InfectionTelemetry.PowerZombieKillRate):+#.##;-#.##;0.00} pp",
            $"Average Match Length: {Delta(result.CurrentRules.Balance.AverageTurns, result.ShieldBlocksPowerZombie.Balance.AverageTurns):+#.##;-#.##;0.00}",
            $"Avg Successful PowerZombie Infections: {Delta(result.CurrentRules.InfectionTelemetry.AverageSuccessfulPowerZombieInfectionsPerMatch, result.ShieldBlocksPowerZombie.InfectionTelemetry.AverageSuccessfulPowerZombieInfectionsPerMatch):+#.##;-#.##;0.00}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatRow(string label, string current, string experimental) =>
        $"{label,-40} | {current,14} | {experimental,14}";

    private static string WinRate(MatchSimulationResult r) => $"{r.Balance.HumanWinRate:F2}%";
    private static string ZombieWinRate(MatchSimulationResult r) => $"{r.Balance.ZombieWinRate:F2}%";
    private static string KillRate(MatchSimulationResult r) => $"{r.InfectionTelemetry.PowerZombieKillRate:F2}%";
    private static string Avg(double value) => $"{value:F2}";
    private static string Rate(double value) => $"{value:F2}%";
    private static double Delta(double a, double b) => Math.Round(b - a, 2);
}
