using ZombieGame.Application.Simulation;
using ZombieGame.Domain.Enums;

var playerCount = 8;
var seed = 42;
if (args.Length >= 1 && int.TryParse(args[0], out var parsedPlayers))
    playerCount = parsedPlayers;
if (args.Length >= 2 && int.TryParse(args[1], out var parsedSeed))
    seed = parsedSeed;

var options = new MatchSimulationOptions
{
    PlayerCount = playerCount,
    MaxTurnsPerMatch = 50,
    RandomSeed = seed,
    UseSuspicionBasedVoting = true,
    MaxPassActionsPerDay = 0
};

var simulator = new MatchSimulator(options);
var random = new Random(seed);
var outcome = await simulator.RunSingleMatchAsync(options, random);
var replay = await simulator.RunDetailedReplayAsync(options, new Random(seed));

var fullReplay = MatchReplayTextFormatter.Format(replay);
var cardReport = BuildPersianCardReport(replay, outcome);

var outputPath = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
    $"replay-{playerCount}players-cards-seed{seed}.txt"));

var combined = new StringBuilder();
combined.AppendLine(cardReport);
combined.AppendLine();
combined.AppendLine(fullReplay);

await ReportFileWriter.WriteUtf8Async(outputPath, combined.ToString());
Console.WriteLine($"Saved: {outputPath}");
Console.WriteLine();
Console.Write(cardReport);

static string BuildPersianCardReport(DetailedMatchReplay replay, SingleMatchOutcome outcome)
{
    var sb = new StringBuilder();
    sb.AppendLine("══════════════════════════════════════════════════════════");
    sb.AppendLine($"  گزارش شبیه‌سازی {replay.PlayerCount} نفره — استفاده از کارت‌ها");
    sb.AppendLine("══════════════════════════════════════════════════════════");
    sb.AppendLine($"Seed: {replay.RandomSeed} | روزها: {replay.TotalDays} | بازیکنان: {replay.PlayerCount}");
    var winnerFa = replay.IsStalemate
        ? "تساوی (سقف روز)"
        : replay.Winner == WinTeam.Humans ? "انسان‌ها" : "زامبی‌ها";
    sb.AppendLine($"نتیجه: {winnerFa}");
    sb.AppendLine($"رویداد روز اول: {replay.StartingDayEvent}");
    sb.AppendLine();

    var totalPasses = outcome.PassCounts.Human + outcome.PassCounts.Zombie + outcome.PassCounts.PowerZombie;
    var totalActions = outcome.TotalCardPlays + totalPasses;
    var passPct = totalActions > 0 ? 100.0 * totalPasses / totalActions : 0;

    sb.AppendLine("── خلاصه آمار ──");
    sb.AppendLine($"  کل بازی کارت: {outcome.TotalCardPlays}");
    sb.AppendLine($"  کل PASS: {totalPasses} ({passPct:F1}%)");
    sb.AppendLine($"    انسان: {outcome.PassCounts.Human} | زامبی: {outcome.PassCounts.Zombie} | PowerZombie: {outcome.PassCounts.PowerZombie}");
    sb.AppendLine($"  حذف با رأی: {outcome.VoteEliminations}");
    sb.AppendLine($"  Friendly fire: {outcome.FriendlyFireCount}");
    sb.AppendLine();

    sb.AppendLine("── تعداد هر نوع کارت ──");
    if (outcome.CardPlaysByEffect.Count == 0)
    {
        sb.AppendLine("  (هیچ کارتی بازی نشد)");
    }
    else
    {
        foreach (var (effect, count) in outcome.CardPlaysByEffect.OrderByDescending(k => k.Value))
            sb.AppendLine($"  {EffectLabelFa(effect),-14} {count,3} بار");
    }
    sb.AppendLine();

    sb.AppendLine("── جزئیات هر بازی کارت (روز به روز) ──");
    var playNum = 0;
    foreach (var day in replay.Days)
    {
        var dayPlays = new List<string>();
        foreach (var battle in day.Battles)
        {
            foreach (var action in new[] { battle.Player1Action, battle.Player2Action })
            {
                if (action.PublicAction != "Action" || string.IsNullOrWhiteSpace(action.Detail))
                    continue;
                if (action.Detail.Equals("Pass", StringComparison.OrdinalIgnoreCase))
                    continue;
                playNum++;
                dayPlays.Add($"    [{playNum,2}] {action.Detail}");
            }
        }

        if (dayPlays.Count == 0)
            continue;

        sb.AppendLine($"  روز {day.DayNumber} ({day.DayEvent}):");
        foreach (var line in dayPlays)
            sb.AppendLine(line);
    }

    if (playNum == 0)
        sb.AppendLine("  (هیچ بازی کارت ثبت نشد)");

    sb.AppendLine();
    sb.AppendLine("── ترکیب PASS در نبردها (هر روز) ──");
    foreach (var day in replay.Days)
    {
        var passes = 0;
        var actions = 0;
        foreach (var battle in day.Battles)
        {
            foreach (var action in new[] { battle.Player1Action, battle.Player2Action })
            {
                if (action.PublicAction == "Pass") passes++;
                else actions++;
            }
        }

        if (battleCount(day) == 0)
            continue;

        sb.AppendLine($"  روز {day.DayNumber}: {actions} اکشن / {passes} PASS از {actions + passes} نوبت");
    }

    sb.AppendLine("══════════════════════════════════════════════════════════");
    return sb.ToString();
}

static int battleCount(ReplayDayLog day) => day.Battles.Count;

static string EffectLabelFa(string effect) => effect.ToLowerInvariant() switch
{
    "shoot" => "شات‌گان",
    "heal" => "مدیکیت",
    "shield" => "سپر",
    "infect" => "عفونت",
    "power_zombie" => "آلفا زامبی",
    _ => effect
};
