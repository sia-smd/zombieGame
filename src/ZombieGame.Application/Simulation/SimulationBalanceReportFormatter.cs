namespace ZombieGame.Application.Simulation;

using System.Text;

public static class SimulationBalanceReportFormatter
{
    public static string FormatComplete(MatchSimulationResult result) =>
        string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            FormatPersian(result),
            FormatEnglish(result),
            FormatInfectionSection(result)
        });

    public static string FormatPersian(MatchSimulationResult result)
    {
        var b = result.Balance;
        var p = result.PassTelemetry;
        var i = result.InfectionTelemetry;
        var v = result.VotingTelemetry;
        var totalDayEvents = b.DayEventOccurrences.Values.Sum();
        var composition = BalanceScenarioProfiles.GetComposition(result.Options.Scenario, result.Options.PlayerCount);
        var lines = new List<string>
        {
            "══════════════════════════════════════════════════════════",
            "  گزارش بالانس — شبیه‌سازی انبوه",
            "══════════════════════════════════════════════════════════",
            $"سناریو: {BalanceScenarioProfiles.GetDisplayNameFa(result.Options.Scenario)}",
            $"ترکیب نقش: {composition.HumanCount(result.Options.PlayerCount)} انسان / {composition.ZombieCount} زامبی / {composition.PowerZombieCount} PowerZombie",
            $"بازی‌ها: {b.TotalMatches:N0} | بازیکنان: {result.Options.PlayerCount} | زمان: {result.Elapsed.TotalSeconds:F1}s ({result.MatchesPerSecond:N0} بازی/ثانیه)",
            $"Seed: {result.Options.RandomSeed?.ToString() ?? "تصادفی"}",
            "",
            "── نتیجه بازی‌ها ──",
            $"  برد انسان‌ها: {b.HumanWinRate:F2}% ({b.HumanWins:N0} بازی)",
            $"  برد زامبی‌ها: {b.ZombieWinRate:F2}% ({b.ZombieWins:N0} بازی)",
            $"  تساوی (سقف روز): {b.StalemateRate:F2}% ({b.Stalemates:N0} بازی)",
            "",
            "── طول بازی (روز) ──",
            $"  میانگین: {b.AverageTurns:F2} روز",
            $"  کمترین: {b.MinTurns} روز",
            $"  بیشترین: {b.MaxTurns} روز",
            "",
            "── میانگین کارت در هر بازی ──",
            $"  کل: {b.AverageCardPlaysPerMatch:F2}",
            $"  شات‌گان: {PerMatch(b, "shoot"):F2}",
            $"  مدیکیت: {PerMatch(b, "heal"):F2}",
            $"  عفونت: {PerMatch(b, "infect"):F2}",
            $"  آلفا زامبی: {PerMatch(b, "power_zombie"):F2}",
            $"  سپر: {PerMatch(b, "shield"):F2}",
            $"  حذف با رأی: {b.AverageEliminationsByVote:F2}",
            $"  Friendly fire: {b.AverageFriendlyFirePerMatch:F2}",
            "",
            "── درصد PASS (از کل نوبت‌های همان نقش: PASS + بازی کارت) ──",
            $"  انسان: {p.HumanActions.PassRatePercent:F2}%  (PASS/بازی: {p.HumanActions.AveragePassesPerMatch:F2}/{p.HumanActions.AverageCardPlaysPerMatch:F2})",
            $"  زامبی: {p.ZombieActions.PassRatePercent:F2}%  (PASS/بازی: {p.ZombieActions.AveragePassesPerMatch:F2}/{p.ZombieActions.AverageCardPlaysPerMatch:F2})",
            $"  PowerZombie: {p.PowerZombieActions.PassRatePercent:F2}%  (PASS/بازی: {p.PowerZombieActions.AveragePassesPerMatch:F2}/{p.PowerZombieActions.AverageCardPlaysPerMatch:F2})",
            "",
            "── عفونت و رأی‌گیری ──",
            $"  نرخ موفقیت عفونت زامبی: {i.InfectionSuccessRate:F2}%",
            $"  نرخ موفقیت آلفا زامبی: {i.PowerZombieInfectionSuccessRate:F2}%",
            $"  میانگین درمان موفق/بازی: {i.AverageHealsPerMatch:F2}",
            $"  میانگین عفونت موفق/بازی: {i.AverageSuccessfulInfectionsPerMatch:F2}",
            $"  حذف با رأی (کل): {v.TotalVoteEliminations:N0} | میانگین/بازی: {b.AverageEliminationsByVote:F2}",
            "",
            "── درصد رویداد روز (تمام روزهای بازی‌شده) ──"
        };

        foreach (var (dayEvent, count) in b.DayEventOccurrences.OrderByDescending(k => k.Value))
        {
            var pct = totalDayEvents > 0 ? count * 100.0 / totalDayEvents : 0;
            lines.Add($"  {DayEventLabelFa(dayEvent),-12} {pct,5:F2}%  ({count:N0} روز)");
        }

        lines.Add("");
        lines.Add("── تعداد کارت‌ها (کل همه بازی‌ها) ──");
        foreach (var (effect, count) in b.CardPlaysByEffect.OrderByDescending(k => k.Value))
            lines.Add($"  {EffectLabelFa(effect),-14} {count,8:N0}");

        lines.Add("");
        lines.Add("══════════════════════════════════════════════════════════");
        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatEnglish(MatchSimulationResult result)
    {
        var b = result.Balance;
        var p = result.PassTelemetry;
        var i = result.InfectionTelemetry;
        var v = result.VotingTelemetry;
        var totalDayEvents = b.DayEventOccurrences.Values.Sum();
        var composition = BalanceScenarioProfiles.GetComposition(result.Options.Scenario, result.Options.PlayerCount);

        var sb = new StringBuilder();
        sb.AppendLine("══════════════════════════════════════════════════════════");
        sb.AppendLine("  BALANCE REPORT — BULK SIMULATION (ENGLISH)");
        sb.AppendLine("══════════════════════════════════════════════════════════");
        sb.AppendLine($"Scenario: {BalanceScenarioProfiles.GetDisplayName(result.Options.Scenario)}");
        sb.AppendLine($"Roles: {composition.HumanCount(result.Options.PlayerCount)} Human / {composition.ZombieCount} Zombie / {composition.PowerZombieCount} PowerZombie");
        sb.AppendLine($"Games: {b.TotalMatches:N0} | Players: {result.Options.PlayerCount} | Elapsed: {result.Elapsed.TotalSeconds:F1}s");
        sb.AppendLine($"Seed: {result.Options.RandomSeed?.ToString() ?? "random"}");
        sb.AppendLine();
        sb.AppendLine("── Outcomes ──");
        sb.AppendLine($"  Human Wins: {b.HumanWinRate:F2}% ({b.HumanWins:N0})");
        sb.AppendLine($"  Zombie Wins: {b.ZombieWinRate:F2}% ({b.ZombieWins:N0})");
        sb.AppendLine($"  Draw / Stalemate: {b.StalemateRate:F2}% ({b.Stalemates:N0})");
        sb.AppendLine();
        sb.AppendLine("── Game Length (Days) ──");
        sb.AppendLine($"  Average: {b.AverageTurns:F2}");
        sb.AppendLine($"  Min: {b.MinTurns}");
        sb.AppendLine($"  Max: {b.MaxTurns}");
        sb.AppendLine();
        sb.AppendLine("── Average Cards Per Match ──");
        sb.AppendLine($"  Total: {b.AverageCardPlaysPerMatch:F2}");
        sb.AppendLine($"  Shotgun: {PerMatch(b, "shoot"):F2}");
        sb.AppendLine($"  Heal: {PerMatch(b, "heal"):F2}");
        sb.AppendLine($"  Infection: {PerMatch(b, "infect"):F2}");
        sb.AppendLine($"  Alpha Zombie: {PerMatch(b, "power_zombie"):F2}");
        sb.AppendLine($"  Shield: {PerMatch(b, "shield"):F2}");
        sb.AppendLine($"  Vote Eliminations: {b.AverageEliminationsByVote:F2}");
        sb.AppendLine($"  Friendly Fire: {b.AverageFriendlyFirePerMatch:F2}");
        sb.AppendLine();
        sb.AppendLine("── PASS Rate By Role ──");
        sb.AppendLine($"  Human: {p.HumanActions.PassRatePercent:F2}%");
        sb.AppendLine($"  Zombie: {p.ZombieActions.PassRatePercent:F2}%");
        sb.AppendLine($"  PowerZombie: {p.PowerZombieActions.PassRatePercent:F2}%");
        sb.AppendLine();
        sb.AppendLine("── Infection & Voting ──");
        sb.AppendLine($"  Zombie Infection Success: {i.InfectionSuccessRate:F2}%");
        sb.AppendLine($"  PowerZombie Infection Success: {i.PowerZombieInfectionSuccessRate:F2}%");
        sb.AppendLine($"  Avg Successful Heals/Match: {i.AverageHealsPerMatch:F2}");
        sb.AppendLine($"  Avg Successful Infections/Match: {i.AverageSuccessfulInfectionsPerMatch:F2}");
        sb.AppendLine($"  Vote Eliminations Total: {v.TotalVoteEliminations:N0}");
        sb.AppendLine();
        sb.AppendLine("── Day Event Distribution ──");
        foreach (var (dayEvent, count) in b.DayEventOccurrences.OrderByDescending(k => k.Value))
        {
            var pct = totalDayEvents > 0 ? count * 100.0 / totalDayEvents : 0;
            sb.AppendLine($"  {dayEvent,-12} {pct,5:F2}%  ({count:N0} days)");
        }

        sb.AppendLine();
        sb.AppendLine("── Card Plays (All Matches) ──");
        foreach (var (effect, count) in b.CardPlaysByEffect.OrderByDescending(k => k.Value))
            sb.AppendLine($"  {effect,-14} {count,8:N0}");

        sb.AppendLine("══════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    public static string FormatScenarioComparison(
        MatchSimulationResult baseline,
        MatchSimulationResult comparison,
        string comparisonLabel)
    {
        var sb = new StringBuilder();
        sb.AppendLine("══════════════════════════════════════════════════════════");
        sb.AppendLine("  SCENARIO COMPARISON");
        sb.AppendLine("══════════════════════════════════════════════════════════");
        sb.AppendLine($"Baseline: {BalanceScenarioProfiles.GetDisplayName(baseline.Options.Scenario)}");
        sb.AppendLine($"Compare:  {comparisonLabel}");
        sb.AppendLine();
        AppendDelta(sb, "Human Win %", baseline.Balance.HumanWinRate, comparison.Balance.HumanWinRate);
        AppendDelta(sb, "Zombie Win %", baseline.Balance.ZombieWinRate, comparison.Balance.ZombieWinRate);
        AppendDelta(sb, "Avg Days", baseline.Balance.AverageTurns, comparison.Balance.AverageTurns);
        AppendDelta(sb, "Avg Cards/Match", baseline.Balance.AverageCardPlaysPerMatch, comparison.Balance.AverageCardPlaysPerMatch);
        AppendDelta(sb, "Shotgun/Match", PerMatch(baseline.Balance, "shoot"), PerMatch(comparison.Balance, "shoot"));
        AppendDelta(sb, "Heal/Match", PerMatch(baseline.Balance, "heal"), PerMatch(comparison.Balance, "heal"));
        AppendDelta(sb, "Infection Success %", baseline.InfectionTelemetry.InfectionSuccessRate, comparison.InfectionTelemetry.InfectionSuccessRate);
        AppendDelta(sb, "Human PASS %", baseline.PassTelemetry.HumanActions.PassRatePercent, comparison.PassTelemetry.HumanActions.PassRatePercent);
        sb.AppendLine("══════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    private static string FormatInfectionSection(MatchSimulationResult result) =>
        InfectionBalanceReportFormatter.Format(result);

    private static double PerMatch(BalanceStatistics balance, string effectKey)
    {
        if (balance.TotalMatches <= 0)
            return 0;
        return Math.Round(balance.CardPlaysByEffect.GetValueOrDefault(effectKey) / (double)balance.TotalMatches, 2);
    }

    private static void AppendDelta(StringBuilder sb, string label, double baseline, double comparison)
    {
        var delta = comparison - baseline;
        sb.AppendLine($"  {label,-22} {baseline,8:F2} → {comparison,8:F2}  (Δ {delta:+0.00;-0.00})");
    }

    private static string DayEventLabelFa(string dayEvent) => dayEvent switch
    {
        "NormalDay" => "روز عادی",
        "SunnyDay" => "روز آفتابی",
        "Storm" => "طوفان",
        _ => dayEvent
    };

    private static string EffectLabelFa(string effect) => effect.ToLowerInvariant() switch
    {
        "shoot" => "شات‌گان",
        "heal" => "مدیکیت",
        "shield" => "سپر",
        "infect" => "عفونت",
        "power_zombie" => "آلفا زامبی",
        _ => effect
    };
}
