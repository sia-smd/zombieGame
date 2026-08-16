namespace ZombieGame.Application.Simulation;

public sealed class RolePassCounts
{
    public int Human { get; init; }
    public int Zombie { get; init; }
    public int PowerZombie { get; init; }

    public int Total => Human + Zombie + PowerZombie;
}

public sealed class RoleCardPlayCounts
{
    public int Human { get; init; }
    public int Zombie { get; init; }
    public int PowerZombie { get; init; }

    public int Total => Human + Zombie + PowerZombie;
}

public sealed class RoleActionUsageStats
{
    public long TotalPasses { get; set; }
    public long TotalCardPlays { get; set; }
    public double AveragePassesPerMatch { get; set; }
    public double AverageCardPlaysPerMatch { get; set; }
    public double PassRatePercent { get; set; }
}

public sealed class RolePassUsageStats
{
    public long TotalPasses { get; set; }
    public double AveragePassesPerMatch { get; set; }
    public double AveragePassesPerDay { get; set; }
    public double AveragePassesPerPlayer { get; set; }
}

public sealed class PassWinRateCorrelation
{
    /// <summary>Pearson correlation between human pass count per match and human win (1) / zombie win (0).</summary>
    public double HumanPassCountVsHumanWin { get; set; }

    /// <summary>Pearson correlation between zombie pass count per match and human win.</summary>
    public double ZombiePassCountVsHumanWin { get; set; }

    /// <summary>Pearson correlation between power-zombie pass count per match and human win.</summary>
    public double PowerZombiePassCountVsHumanWin { get; set; }

    /// <summary>Pearson correlation between total pass count per match and human win.</summary>
    public double TotalPassCountVsHumanWin { get; set; }
}

public sealed class PassUsageTelemetry
{
    public long TotalDays { get; set; }
    public RolePassUsageStats Humans { get; set; } = new();
    public RolePassUsageStats Zombies { get; set; } = new();
    public RolePassUsageStats PowerZombies { get; set; } = new();
    public RoleActionUsageStats HumanActions { get; set; } = new();
    public RoleActionUsageStats ZombieActions { get; set; } = new();
    public RoleActionUsageStats PowerZombieActions { get; set; } = new();
    public PassWinRateCorrelation Correlation { get; set; } = new();
}

public static class SimulationReportFormatter
{
    public static string FormatPassUsageReport(MatchSimulationResult result)
    {
        var t = result.PassTelemetry;
        var b = result.Balance;
        var lines = new List<string>
        {
            "=== Pass Usage By Role ===",
            $"Simulations: {b.TotalMatches:N0} | Days played: {t.TotalDays:N0} | Elapsed: {result.Elapsed.TotalSeconds:F2}s",
            "",
            "Humans:",
            $"  Total Passes: {t.Humans.TotalPasses:N0}",
            $"  Average Passes Per Match: {t.Humans.AveragePassesPerMatch:F4}",
            $"  Average Passes Per Day: {t.Humans.AveragePassesPerDay:F4}",
            $"  Average Passes Per Human Player: {t.Humans.AveragePassesPerPlayer:F4}",
            "",
            "Zombies:",
            $"  Total Passes: {t.Zombies.TotalPasses:N0}",
            $"  Average Passes Per Match: {t.Zombies.AveragePassesPerMatch:F4}",
            $"  Average Passes Per Day: {t.Zombies.AveragePassesPerDay:F4}",
            $"  Average Passes Per Zombie Player: {t.Zombies.AveragePassesPerPlayer:F4}",
            "",
            "PowerZombies:",
            $"  Total Passes: {t.PowerZombies.TotalPasses:N0}",
            $"  Average Passes Per Match: {t.PowerZombies.AveragePassesPerMatch:F4}",
            $"  Average Passes Per Day: {t.PowerZombies.AveragePassesPerDay:F4}",
            $"  Average Passes Per PowerZombie Player: {t.PowerZombies.AveragePassesPerPlayer:F4}",
            "",
            "=== Pass Usage vs Win Rate (Pearson r, outcome: 1=Human win, 0=Zombie win) ===",
            $"  Human Pass Count: {t.Correlation.HumanPassCountVsHumanWin:F4}",
            $"  Zombie Pass Count: {t.Correlation.ZombiePassCountVsHumanWin:F4}",
            $"  PowerZombie Pass Count: {t.Correlation.PowerZombiePassCountVsHumanWin:F4}",
            $"  Total Pass Count: {t.Correlation.TotalPassCountVsHumanWin:F4}",
            "",
            "=== Match Outcomes ===",
            $"  Human Win Rate: {b.HumanWinRate:F2}%",
            $"  Zombie Win Rate: {b.ZombieWinRate:F2}%",
            $"  Average Turns (Days): {b.AverageTurns:F2}",
            "",
            "=== Passes Per Match (Summary) ===",
            $"  Human Passes/Match: {t.Humans.AveragePassesPerMatch:F4}",
            $"  Zombie Passes/Match: {t.Zombies.AveragePassesPerMatch:F4}",
            $"  PowerZombie Passes/Match: {t.PowerZombies.AveragePassesPerMatch:F4}"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
