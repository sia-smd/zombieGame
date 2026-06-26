namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;

public sealed class InfectionMatchCounts
{
    public int SuccessfulZombieInfections { get; init; }
    public int BlockedZombieInfections { get; init; }
    public int SuccessfulPowerZombieInfections { get; init; }
    public int ZombieCures { get; init; }
    public int HumansConvertedToZombies { get; init; }
    public int ZombiesConvertedToHumans { get; init; }
    public int HealCardPlays { get; init; }
    public int PowerZombieCardPlays { get; init; }
    public int BlockedPowerZombieInfections { get; init; }
    public int PowerZombieEliminations { get; init; }
    public int InfectedWithRemainingShield { get; init; }
    public int DisabledShotgunsAfterInfection { get; init; }
    public int DisabledHealsAfterInfection { get; init; }
}

public sealed class InfectionTelemetry
{
    public long TotalSuccessfulZombieInfections { get; set; }
    public long TotalBlockedZombieInfections { get; set; }
    public long TotalSuccessfulPowerZombieInfections { get; set; }
    public long TotalZombieCures { get; set; }
    public long TotalHumansConvertedToZombies { get; set; }
    public long TotalZombiesConvertedToHumans { get; set; }
    public long TotalHealCardPlays { get; set; }
    public long TotalPowerZombieCardPlays { get; set; }

    public double AverageSuccessfulInfectionsPerMatch { get; set; }
    public double AverageFailedInfectionsPerMatch { get; set; }
    public double AverageHealsPerMatch { get; set; }
    public double AverageHumanToZombieConversionsPerMatch { get; set; }
    public double AverageZombieToHumanConversionsPerMatch { get; set; }

    public double InfectionSuccessRate { get; set; }
    public double PowerZombieInfectionSuccessRate { get; set; }
    public double AverageSuccessfulPowerZombieInfectionsPerMatch { get; set; }
    public double AverageBlockedPowerZombieInfectionsPerMatch { get; set; }
    public double PowerZombieKillRate { get; set; }
    public double HealEffectivenessRate { get; set; }
    public long TotalInfectedWithRemainingShield { get; set; }
    public long TotalDisabledShotgunsAfterInfection { get; set; }
    public long TotalDisabledHealsAfterInfection { get; set; }
    public double AverageInfectedWithRemainingShieldPerMatch { get; set; }
    public double AverageDisabledShotgunsAfterInfectionPerMatch { get; set; }
    public double AverageDisabledHealsAfterInfectionPerMatch { get; set; }
}

public static class InfectionBalanceReportFormatter
{
    public static string Format(MatchSimulationResult result)
    {
        var b = result.Balance;
        var i = result.InfectionTelemetry;
        var p = result.PassTelemetry;

        var lines = new List<string>
        {
            "=== Infection Balance Report ===",
            $"Simulations: {b.TotalMatches:N0} | Elapsed: {result.Elapsed.TotalSeconds:F2}s | {result.MatchesPerSecond:N0} matches/s",
            "",
            "=== Match Outcomes ===",
            $"  Human Win Rate: {b.HumanWinRate:F2}%",
            $"  Zombie Win Rate: {b.ZombieWinRate:F2}%",
            $"  Stalemate Rate: {b.StalemateRate:F2}%",
            $"  Average Match Length (Days): {b.AverageTurns:F2}",
            $"  Average Card Plays/Match: {b.AverageCardPlaysPerMatch:F2}",
            $"  Average Vote Eliminations/Match: {b.AverageEliminationsByVote:F2}",
            "",
            "=== Infection Totals (All Matches) ===",
            $"  Successful Zombie Infections: {i.TotalSuccessfulZombieInfections:N0}",
            $"  Blocked Zombie Infections (Shield): {i.TotalBlockedZombieInfections:N0}",
            $"  Successful PowerZombie Infections: {i.TotalSuccessfulPowerZombieInfections:N0}",
            $"  Zombie Cures (Heal): {i.TotalZombieCures:N0}",
            $"  Humans Converted To Zombies: {i.TotalHumansConvertedToZombies:N0}",
            $"  Zombies Converted Back To Humans: {i.TotalZombiesConvertedToHumans:N0}",
            "",
            "=== Infection Averages Per Match ===",
            $"  Average Successful Infections: {i.AverageSuccessfulInfectionsPerMatch:F4}",
            $"  Average Failed Infections: {i.AverageFailedInfectionsPerMatch:F4}",
            $"  Average Heals (Zombie Cures): {i.AverageHealsPerMatch:F4}",
            $"  Average Human→Zombie Conversions: {i.AverageHumanToZombieConversionsPerMatch:F4}",
            $"  Average Zombie→Human Conversions: {i.AverageZombieToHumanConversionsPerMatch:F4}",
            "",
            "=== Effectiveness Rates ===",
            $"  Infection Success Rate: {i.InfectionSuccessRate:F2}%",
            $"  PowerZombie Infection Success Rate: {i.PowerZombieInfectionSuccessRate:F2}%",
            $"  Heal Effectiveness Rate: {i.HealEffectivenessRate:F2}%",
            "",
            "=== Infection Transformation (Per Match) ===",
            $"  Infected With Remaining Shield: {i.AverageInfectedWithRemainingShieldPerMatch:F4}",
            $"  Disabled Shotguns After Infection: {i.AverageDisabledShotgunsAfterInfectionPerMatch:F4}",
            $"  Disabled Heals After Infection: {i.AverageDisabledHealsAfterInfectionPerMatch:F4}",
            "",
            "=== Pass Usage (Per Match) ===",
            $"  Human Passes/Match: {p.Humans.AveragePassesPerMatch:F4}",
            $"  Zombie Passes/Match: {p.Zombies.AveragePassesPerMatch:F4}",
            $"  PowerZombie Passes/Match: {p.PowerZombies.AveragePassesPerMatch:F4}",
            "",
            "=== Card Plays By Effect (All Matches) ==="
        };

        foreach (var (effect, count) in b.CardPlaysByEffect.OrderByDescending(kvp => kvp.Value))
            lines.Add($"  {effect}: {count:N0}");

        return string.Join(Environment.NewLine, lines);
    }
}

public static class InfectionTelemetryRecorder
{
    public static void Record(
        MutableInfectionMatchCounts counts,
        string effectKey,
        CardEffectTelemetryKind telemetryKind,
        InfectionTransformResult? transform = null)
    {
        if (effectKey.Equals("heal", StringComparison.OrdinalIgnoreCase))
            counts.HealCardPlays++;

        if (effectKey.Equals("power_zombie", StringComparison.OrdinalIgnoreCase))
            counts.PowerZombieCardPlays++;

        switch (telemetryKind)
        {
            case CardEffectTelemetryKind.ZombieInfectionSucceeded:
                counts.SuccessfulZombieInfections++;
                counts.HumansConvertedToZombies++;
                break;
            case CardEffectTelemetryKind.ZombieInfectionBlockedByShield:
                counts.BlockedZombieInfections++;
                break;
            case CardEffectTelemetryKind.PowerZombieInfectionSucceeded:
                counts.SuccessfulPowerZombieInfections++;
                counts.HumansConvertedToZombies++;
                break;
            case CardEffectTelemetryKind.PowerZombieInfectionBlockedByShield:
                counts.BlockedPowerZombieInfections++;
                break;
            case CardEffectTelemetryKind.ZombieCured:
                counts.ZombieCures++;
                counts.ZombiesConvertedToHumans++;
                break;
        }

        if (transform is not null)
        {
            if (transform.HasRemainingShieldCard)
                counts.InfectedWithRemainingShield++;
            counts.DisabledShotgunsAfterInfection += transform.DisabledShotguns;
            counts.DisabledHealsAfterInfection += transform.DisabledHeals;
        }
    }

    public static InfectionMatchCounts ToImmutable(MutableInfectionMatchCounts counts) =>
        new()
        {
            SuccessfulZombieInfections = counts.SuccessfulZombieInfections,
            BlockedZombieInfections = counts.BlockedZombieInfections,
            SuccessfulPowerZombieInfections = counts.SuccessfulPowerZombieInfections,
            ZombieCures = counts.ZombieCures,
            HumansConvertedToZombies = counts.HumansConvertedToZombies,
            ZombiesConvertedToHumans = counts.ZombiesConvertedToHumans,
            HealCardPlays = counts.HealCardPlays,
            PowerZombieCardPlays = counts.PowerZombieCardPlays,
            BlockedPowerZombieInfections = counts.BlockedPowerZombieInfections,
            PowerZombieEliminations = counts.PowerZombieEliminations,
            InfectedWithRemainingShield = counts.InfectedWithRemainingShield,
            DisabledShotgunsAfterInfection = counts.DisabledShotgunsAfterInfection,
            DisabledHealsAfterInfection = counts.DisabledHealsAfterInfection
        };
}

public sealed class MutableInfectionMatchCounts
{
    public int SuccessfulZombieInfections { get; set; }
    public int BlockedZombieInfections { get; set; }
    public int SuccessfulPowerZombieInfections { get; set; }
    public int BlockedPowerZombieInfections { get; set; }
    public int PowerZombieEliminations { get; set; }
    public int ZombieCures { get; set; }
    public int HumansConvertedToZombies { get; set; }
    public int ZombiesConvertedToHumans { get; set; }
    public int HealCardPlays { get; set; }
    public int PowerZombieCardPlays { get; set; }
    public int InfectedWithRemainingShield { get; set; }
    public int DisabledShotgunsAfterInfection { get; set; }
    public int DisabledHealsAfterInfection { get; set; }
}
