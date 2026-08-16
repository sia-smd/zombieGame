using System.Text;
using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.Simulation;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

enum BattleAction
{
    Pass,
    HumanRole,
    Shield,
    Shoot,
    Heal,
    Infect
}

sealed record ScenarioResult(
    string HumanPlan,
    string ZombiePlan,
    string TurnOrder,
    List<string> Events,
    string HumanRole,
    string ZombieRole,
    bool HumanAlive,
    bool ZombieAlive);

sealed record ActionPlan(string LabelFa, BattleAction Slot1, BattleAction Slot2);

static class Program
{
    private static readonly ActionPlan[] HumanPlans =
    [
        new("انسان ، سپر", BattleAction.HumanRole, BattleAction.Shield),       // Human, Shield
        new("انسان ، درمان", BattleAction.HumanRole, BattleAction.Heal),       // Human, Heal
        new("انسان ، شات‌گان", BattleAction.HumanRole, BattleAction.Shoot),   // Human, Shotgun
        new("انسان ، پاس", BattleAction.HumanRole, BattleAction.Pass),         // Human, Pass
        new("سپر ، درمان", BattleAction.Shield, BattleAction.Heal),
        new("سپر ، شات‌گان", BattleAction.Shield, BattleAction.Shoot),
        new("سپر ، پاس", BattleAction.Shield, BattleAction.Pass),
        new("درمان ، شات‌گان", BattleAction.Heal, BattleAction.Shoot),
        new("درمان ، پاس", BattleAction.Heal, BattleAction.Pass),
        new("شات‌گان ، شات‌گان", BattleAction.Shoot, BattleAction.Shoot),
        new("شات‌گان ، پاس", BattleAction.Shoot, BattleAction.Pass),
        new("پاس ، پاس", BattleAction.Pass, BattleAction.Pass)                 // Pass, Pass
    ];

    private static readonly ActionPlan[] ZombiePlans =
    [
        new("مسمومیت ، سپر", BattleAction.Infect, BattleAction.Shield),     // Zombie, Shield
        new("مسمومیت ، پاس", BattleAction.Infect, BattleAction.Pass),       // Zombie, Pass
        new("سپر ، پاس", BattleAction.Shield, BattleAction.Pass),
        new("پاس ، پاس", BattleAction.Pass, BattleAction.Pass)
    ];

    private static IEnumerable<ActionPlan> DistinctHumanPlans() =>
        HumanPlans.DistinctBy(p => (p.Slot1, p.Slot2));

    private static IEnumerable<ActionPlan> DistinctZombiePlans() =>
        ZombiePlans.DistinctBy(p => (p.Slot1, p.Slot2));

    private static readonly Guid HumanId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ZombieId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly CardDefinition Shotgun = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
        Name = "Shotgun",
        Type = CardType.Shotgun,
        EffectKey = "shoot"
    };

    private static readonly CardDefinition Heal = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
        Name = "Medkit",
        Type = CardType.Heal,
        EffectKey = "heal"
    };

    private static readonly CardDefinition Shield = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
        Name = "Barricade",
        Type = CardType.Shield,
        EffectKey = "shield"
    };

    private static readonly CardDefinition HumanRoleCard = new()
    {
        Id = RoleCardCatalog.Human,
        Name = "Human",
        Type = CardType.Human,
        EffectKey = "human_role"
    };

    private static readonly CardDefinition Infection = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
        Name = "Infection",
        Type = CardType.Zombie,
        EffectKey = "infect"
    };

    static int Main()
    {
        var dayEvents = new[] { DayEventType.NormalDay, DayEventType.SunnyDay, DayEventType.Storm };
        foreach (var dayEvent in dayEvents)
        {
            var outputPath = RunForDayEvent(dayEvent);
            Console.WriteLine($"Report written (UTF-8 BOM): {outputPath}");
        }

        return 0;
    }

    private static string RunForDayEvent(DayEventType dayEvent)
    {
        var report = new StringBuilder();
        AppendDayHeader(report, dayEvent);
        AppendPlanCatalog(report);

        var orderings = BuildOrderings();
        var results = new List<ScenarioResult>();

        foreach (var human in DistinctHumanPlans())
        foreach (var zombie in DistinctZombiePlans())
        {
            var plans = new Dictionary<string, BattleAction>
            {
                ["H1"] = human.Slot1,
                ["H2"] = human.Slot2,
                ["Z1"] = zombie.Slot1,
                ["Z2"] = zombie.Slot2
            };

            foreach (var order in orderings)
            {
                var outcome = Simulate(dayEvent, plans, order);
                var orderLabel = string.Join(" → ", order);
                results.Add(new ScenarioResult(
                    human.LabelFa,
                    zombie.LabelFa,
                    orderLabel,
                    outcome.Events,
                    RoleFa(outcome.HumanRole),
                    RoleFa(outcome.ZombieRole),
                    outcome.HumanAlive,
                    outcome.ZombieAlive));
            }
        }

        AppendUserExamplesVerification(report, dayEvent);
        AppendGroupedReport(report, results, dayEvent);
        AppendOutcomeSummary(report, results, dayEvent);
        AppendFullScenarioList(report, results, dayEvent);

        var outputPath = ResolveReportPath(dayEvent);
        ReportFileWriter.WriteUtf8(outputPath, report.ToString());
        return outputPath;
    }

    private static void AppendDayHeader(StringBuilder report, DayEventType dayEvent)
    {
        report.AppendLine($"=== شبیه‌سازی ترکیب کارت‌ها — {DayEventFa(dayEvent)} ===");
        report.AppendLine();
        report.AppendLine("قوانین این روز:");
        foreach (var rule in DayRulesFa(dayEvent))
            report.AppendLine($"  • {rule}");
        report.AppendLine();
    }

    private static void AppendPlanCatalog(StringBuilder report)
    {
        report.AppendLine("=== جایگشت‌های مجاز ===");
        report.AppendLine();
        report.AppendLine("انسان (۱۲ ترکیب):");
        foreach (var plan in HumanPlans)
            report.AppendLine($"  • {plan.LabelFa}");
        report.AppendLine();
        report.AppendLine("زامبی (۴ ترکیب):");
        foreach (var plan in ZombiePlans)
            report.AppendLine($"  • {plan.LabelFa}");
        report.AppendLine();
        report.AppendLine($"ترکیب‌های یکتا: انسان {DistinctHumanPlans().Count()} × زامبی {DistinctZombiePlans().Count()} = {DistinctHumanPlans().Count() * DistinctZombiePlans().Count()} جفت");
        report.AppendLine("یادداشت: «انسان» = کارت نقش انسان (بدون اثر، فقط انسان‌ها — پاس نیست)");
        report.AppendLine();
    }

    private static IEnumerable<string> DayRulesFa(DayEventType dayEvent) => dayEvent switch
    {
        DayEventType.SunnyDay =>
        [
            "پاس روی کارت اول = پاس کل دور",
            "کارت انسان (نقش): بدون اثر — فقط انسان‌ها",
            "پاورزامبی در روز آفتابی نمی‌تواند حمله کند (در این شبیه‌سازی: انسان در برابر زامبی معمولی)",
            "اولویت کارت: سپر > درمان > شات‌گان > مسمومیت",
            "سپر در دست + اکشن باقی‌مانده = بلاک مسمومیت حتی قبل از بازی سپر"
        ],
        DayEventType.Storm =>
        [
            "پاس روی کارت اول = پاس کل دور",
            "کارت انسان (نقش): بدون اثر — فقط انسان‌ها",
            "زامبی معمولی برای کشته شدن با شات‌گان به ۲ ضربه نیاز دارد",
            "اولویت کارت: سپر > درمان > شات‌گان > مسمومیت",
            "سپر فقط جلوی مسمومیت پاورزامبی را می‌گیرد (نه زامبی معمولی)"
        ],
        _ =>
        [
            "پاس روی کارت اول = پاس کل دور (هر دو اکشن مصرف می‌شود)",
            "کارت انسان (نقش): بدون اثر — فقط انسان‌ها؛ پاس محسوب نمی‌شود و دور را تمام نمی‌کند",
            "زامبی‌ها کارت بی‌اثر معادل ندارند",
            "اولویت کارت: سپر > درمان > شات‌گان > مسمومیت",
            "سپر در دست + اکشن باقی‌مانده = بلاک مسمومیت حتی قبل از بازی سپر",
            "درمان فقط روی کسی که امروز حمله کرده اثر دارد"
        ]
    };

    private static string ResolveReportPath(DayEventType dayEvent)
    {
        var fileName = $"card-combo-report-{dayEvent}.txt";
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var reportsDir = Path.Combine(projectDir, "reports");
        Directory.CreateDirectory(reportsDir);
        return Path.Combine(reportsDir, fileName);
    }

    private static void AppendOutcomeSummary(StringBuilder report, List<ScenarioResult> results, DayEventType dayEvent)
    {
        var hz = results.Where(r => r.TurnOrder == "H1 → Z1 → H2 → Z2").ToList();
        report.AppendLine();
        report.AppendLine($"=== خلاصه ({hz.Count} ترکیب، ترتیب انسان→زامبی→انسان→زامبی، {DayEventFa(dayEvent)}) ===");
        report.AppendLine();

        var groups = hz
            .GroupBy(r => $"{r.HumanRole}/{r.ZombieRole}|H alive:{r.HumanAlive}|Z alive:{r.ZombieAlive}")
            .OrderByDescending(g => g.Count());

        foreach (var g in groups)
        {
            report.AppendLine($"[{g.Count()} سناریو] نهایی: انسان={g.First().HumanRole}{(g.First().HumanAlive ? "" : " (مرده)")} | زامبی={g.First().ZombieRole}{(g.First().ZombieAlive ? "" : " (مرده)")}");
            foreach (var sample in g.Take(2))
                report.AppendLine($"  نمونه: [{sample.HumanPlan}] | [{sample.ZombiePlan}]");
            report.AppendLine();
        }
    }

    private static void AppendUserExamplesVerification(StringBuilder report, DayEventType dayEvent)
    {
        report.AppendLine("=== بررسی مثال‌های کاربر ===");
        report.AppendLine();
        AppendExample(report, dayEvent, "سپر ، پاس", "مسمومیت ، پاس",
            new Dictionary<string, BattleAction>
            {
                ["H1"] = BattleAction.Shield,
                ["Z1"] = BattleAction.Infect,
                ["H2"] = BattleAction.Pass,
                ["Z2"] = BattleAction.Pass
            },
            new[] { "H1", "Z1", "H2", "Z2" });

        AppendExample(report, dayEvent, "شات‌گان ، پاس", "سپر ، مسمومیت",
            new Dictionary<string, BattleAction>
            {
                ["Z1"] = BattleAction.Shield,
                ["H1"] = BattleAction.Shoot,
                ["H2"] = BattleAction.Pass,
                ["Z2"] = BattleAction.Infect
            },
            new[] { "Z1", "H1", "H2", "Z2" });

        AppendExample(report, dayEvent, "شات‌گان ، سپر", "سپر ، مسمومیت",
            new Dictionary<string, BattleAction>
            {
                ["Z1"] = BattleAction.Shield,
                ["H1"] = BattleAction.Shoot,
                ["H2"] = BattleAction.Shield,
                ["Z2"] = BattleAction.Infect
            },
            new[] { "Z1", "H1", "H2", "Z2" });

        report.AppendLine();
    }

    private static void AppendExample(
        StringBuilder report,
        DayEventType dayEvent,
        string humanPlan,
        string zombiePlan,
        Dictionary<string, BattleAction> plans,
        IReadOnlyList<string> order)
    {
        var outcome = Simulate(dayEvent, plans, order);
        var orderFa = string.Join(" → ", order.Select(SlotFa));
        report.AppendLine($"ترتیب: {orderFa}");
        report.AppendLine($"انسان {humanPlan}");
        report.AppendLine($"زامبی {zombiePlan}");
        report.AppendLine("نتیجه");
        foreach (var e in outcome.Events)
            report.AppendLine(e);
        report.AppendLine($"انسان -> {RoleFa(outcome.HumanRole)}{(outcome.HumanAlive ? "" : " (مرده)")}");
        report.AppendLine($"زامبی -> {RoleFa(outcome.ZombieRole)}{(outcome.ZombieAlive ? "" : " (مرده)")}");
        report.AppendLine("---------------------");
    }

    private static void AppendGroupedReport(StringBuilder report, List<ScenarioResult> results, DayEventType dayEvent)
    {
        report.AppendLine($"=== همه ترکیب‌ها — ترتیب نوبت: انسان، زامبی، انسان، زامبی ({DayEventFa(dayEvent)}) ===");
        report.AppendLine();

        var filtered = results
            .Where(r => r.TurnOrder == "H1 → Z1 → H2 → Z2")
            .OrderBy(r => r.HumanPlan)
            .ThenBy(r => r.ZombiePlan)
            .ToList();

        var grouped = filtered
            .GroupBy(r => (r.HumanPlan, r.ZombiePlan, OutcomeKey(r)))
            .OrderBy(g => g.Key.HumanPlan)
            .ThenBy(g => g.Key.ZombiePlan);

        var index = 1;
        foreach (var group in grouped)
        {
            var sample = group.First();
            report.AppendLine($"#{index++}");
            report.AppendLine(sample.HumanPlan);
            report.AppendLine(sample.ZombiePlan);
            report.AppendLine("نتیجه");
            foreach (var e in sample.Events)
                report.AppendLine(e);
            report.AppendLine($"انسان -> {sample.HumanRole}{(sample.HumanAlive ? "" : " (مرده)")}");
            report.AppendLine($"زامبی -> {sample.ZombieRole}{(sample.ZombieAlive ? "" : " (مرده)")}");
            report.AppendLine("---------------------");
        }

        var multiOrder = results
            .GroupBy(r => (r.HumanPlan, r.ZombiePlan))
            .Where(g => g.Select(x => OutcomeKey(x)).Distinct().Count() > 1)
            .ToList();

        if (multiOrder.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("=== ترکیب‌هایی که با تغییر ترتیب نوبت، نتیجه متفاوت می‌شود ===");
            report.AppendLine();
            foreach (var g in multiOrder.OrderBy(x => x.Key.HumanPlan).ThenBy(x => x.Key.ZombiePlan))
            {
                report.AppendLine($"{g.Key.HumanPlan} | {g.Key.ZombiePlan}");
                foreach (var item in g.OrderBy(x => x.TurnOrder))
                {
                    report.AppendLine($"  [{item.TurnOrder}] انسان->{item.HumanRole} زامبی->{item.ZombieRole} | {string.Join(" ؛ ", item.Events)}");
                }
                report.AppendLine("---------------------");
            }
        }
    }

    private static string OutcomeKey(ScenarioResult r) =>
        $"{r.HumanRole}|{r.ZombieRole}|{r.HumanAlive}|{r.ZombieAlive}|{string.Join(",", r.Events)}";

    private static void AppendFullScenarioList(StringBuilder report, List<ScenarioResult> results, DayEventType dayEvent)
    {
        report.AppendLine();
        report.AppendLine("=== فهرست کامل همه سناریوها (ترتیب انسان، زامبی، انسان، زامبی) ===");
        report.AppendLine();
        report.AppendLine($"روز: {DayEventFa(dayEvent)} ({dayEvent})");
        report.AppendLine($"تعداد کل سناریوها (با همه ترتیب‌ها): {results.Count}");
        report.AppendLine();
        foreach (var r in results.Where(x => x.TurnOrder == "H1 → Z1 → H2 → Z2"))
        {
            report.AppendLine($"{r.HumanPlan} | {r.ZombiePlan}");
            report.AppendLine(string.Join(Environment.NewLine, r.Events));
            report.AppendLine($"انسان -> {r.HumanRole} | زامبی -> {r.ZombieRole}");
            report.AppendLine("-----");
        }
    }

    private static List<IReadOnlyList<string>> BuildOrderings()
    {
        var slots = new[] { "H1", "Z1", "H2", "Z2" };
        return Permute(slots).Select(p => (IReadOnlyList<string>)p.ToList()).ToList();
    }

    private static IEnumerable<IReadOnlyList<string>> Permute(string[] items)
    {
        if (items.Length == 0)
        {
            yield return Array.Empty<string>();
            yield break;
        }

        foreach (var pick in items)
        {
            var rest = items.Where(x => x != pick).ToArray();
            foreach (var tail in Permute(rest))
                yield return new[] { pick }.Concat(tail).ToArray();
        }
    }

    private sealed record SimOutcome(
        List<string> Events,
        PlayerRole HumanRole,
        PlayerRole ZombieRole,
        bool HumanAlive,
        bool ZombieAlive);

    private static SimOutcome Simulate(
        DayEventType dayEvent,
        Dictionary<string, BattleAction> plans,
        IReadOnlyList<string> order)
    {
        var engine = CreateEngine();
        var state = CreateState(dayEvent);
        var events = new List<string>();

        foreach (var slot in order)
            ApplySlotAction(engine, state, events, slot, plans[slot]);

        events = events
            .Distinct()
            .Where(e => !e.Contains(" — Heal had no effect") && !e.Contains(" — Shield already active"))
            .ToList();

        var human = state.GetPlayer(HumanId)!;
        var zombie = state.GetPlayer(ZombieId)!;
        return new SimOutcome(events, human.Role, zombie.Role, human.IsAlive, zombie.IsAlive);
    }

    private static void ApplySlotAction(
        GameRulesEngine engine,
        GameSessionState state,
        List<string> events,
        string slot,
        BattleAction action)
    {
        var isHuman = slot.StartsWith('H');
        var actorId = isHuman ? HumanId : ZombieId;
        var actor = state.GetPlayer(actorId)!;

        if (!GameCombatRules.CanTakeAction(actor))
        {
            if (action != BattleAction.Pass)
                events.Add($"{ActorFa(isHuman)}: دور تمام شد — {Label(action)} انجام نشد");
            return;
        }

        if (!actor.IsAlive)
        {
            events.Add($"{ActorFa(isHuman)}: مرده — {Label(action)} انجام نشد");
            return;
        }

        if (action == BattleAction.Pass)
        {
            var endedTurn = GameCombatRules.IsFirstActionOfTurn(actor);
            engine.PassAction(state, actorId);
            events.Add(endedTurn
                ? $"{ActorFa(isHuman)}: پاس — کل دور پاس شد"
                : $"{ActorFa(isHuman)}: پاس");
            return;
        }

        var card = CardFor(action);
        var targetId = action is BattleAction.Shield or BattleAction.HumanRole
            ? actorId
            : isHuman ? ZombieId : HumanId;
        if (action != BattleAction.HumanRole)
            EnsureCardInHand(state, actorId, card);

        try
        {
            var beforeHumanRole = state.GetPlayer(HumanId)!.Role;
            var beforeZombieRole = state.GetPlayer(ZombieId)!.Role;
            var beforeHumanShield = state.GetPlayer(HumanId)!.HasShield;
            var beforeZombieShield = state.GetPlayer(ZombieId)!.HasShield;

            var result = engine.PlayCard(state, actorId, card, targetId);
            events.Add($"{ActorFa(isHuman)}: {Label(action)} — {Describe(result)}");

            if (beforeHumanShield && !state.GetPlayer(HumanId)!.HasShield && action != BattleAction.Shield)
                events.Add("شکستن سپر انسان");
            if (beforeZombieShield && !state.GetPlayer(ZombieId)!.HasShield && action != BattleAction.Shield)
                events.Add("شکستن سپر زامبی");

            if (beforeHumanRole != state.GetPlayer(HumanId)!.Role)
                events.Add($"تغییر نقش انسان: {RoleFa(beforeHumanRole)} -> {RoleFa(state.GetPlayer(HumanId)!.Role)}");
            if (beforeZombieRole != state.GetPlayer(ZombieId)!.Role)
                events.Add($"تغییر نقش زامبی: {RoleFa(beforeZombieRole)} -> {RoleFa(state.GetPlayer(ZombieId)!.Role)}");

            if (result.TargetKilled)
            {
                var killed = state.GetPlayer(targetId);
                if (killed?.UserId == HumanId)
                    events.Add("کشتن انسان");
                else
                    events.Add("کشتن زامبی");
            }
        }
        catch (Exception ex)
        {
            events.Add($"{ActorFa(isHuman)}: {Label(action)} — ناموفق ({ex.Message})");
        }
    }

    private static string Describe(CardEffectResult result) => result.Message switch
    {
        var m when m.Contains("Shield absorbed") => "سپر جذب شد",
        var m when m.Contains("Shield blocked infection") => "سپر جلوی مسمومیت را گرفت",
        var m when m.Contains("defensive priority") => "اولویت دفاعی — مسمومیت بلاک شد",
        var m when m.Contains("Heal priority") => "اولویت درمان — زامبی درمان شد قبل از مسمومیت",
        var m when m.Contains("Human presence") => "بدون اثر (کارت انسان)",
        var m when m.Contains("Shield activated") => "سپر فعال شد",
        var m when m.Contains("Human was killed") => "انسان کشته شد",
        var m when m.Contains("Zombie eliminated") => "زامبی کشته شد",
        var m when m.Contains("Zombie cured") => "زامبی درمان شد",
        var m when m.Contains("PowerZombie demoted") => "پاورزامبی تنزل یافت",
        var m when m.Contains("Human infected") => "انسان مسموم شد",
        var m when m.StartsWith("Zombie hit (") => DescribeZombieHit(m),
        var m when m.Contains("no effect") => "بی‌اثر",
        _ => result.Message
    };

    private static string DescribeZombieHit(string message)
    {
        var open = message.IndexOf('(');
        var close = message.IndexOf(')');
        if (open < 0 || close <= open)
            return "زامبی اصابت خورد";

        var parts = message[(open + 1)..close].Split('/');
        return parts.Length == 2
            ? $"زامبی اصابت خورد ({parts[0]}/{parts[1]})"
            : "زامبی اصابت خورد";
    }

    private static string DayEventFa(DayEventType dayEvent) => dayEvent switch
    {
        DayEventType.NormalDay => "روز عادی",
        DayEventType.SunnyDay => "روز آفتابی",
        DayEventType.Storm => "روز طوفانی",
        _ => dayEvent.ToString()
    };

    private static GameRulesEngine CreateEngine()
    {
        var dayEvents = GameRulesComposition.CreateDayEventService();
        var registry = new SimpleRegistry();
        var transformation = GameRulesComposition.CreateTransformationService(registry);
        var consumption = new CardConsumptionService();
        var resolver = GameRulesComposition.CreateCardEffectResolver(dayEvents, transformation, registry, consumption);

        return new GameRulesEngine(
            new NoOpRoleAssignment(),
            new NoOpDealing(),
            dayEvents,
            resolver,
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new NoOpCompletion(),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));
    }

    private static GameSessionState CreateState(DayEventType dayEvent)
    {
        var state = new GameSessionState
        {
            MatchId = Guid.NewGuid(),
            CurrentPhase = GamePhase.Day,
            CurrentDayEvent = dayEvent,
            Players =
            [
                new GamePlayerState
                {
                    UserId = HumanId,
                    Username = "Human",
                    Role = PlayerRole.Human,
                    IsAlive = true,
                    SeatIndex = 0,
                    ActionsPerTurn = 2,
                    ActionsUsedThisTurn = 0
                },
                new GamePlayerState
                {
                    UserId = ZombieId,
                    Username = "Zombie",
                    Role = PlayerRole.Zombie,
                    IsAlive = true,
                    SeatIndex = 1,
                    ActionsPerTurn = 2,
                    ActionsUsedThisTurn = 0
                }
            ],
            PlayerHands =
            [
                new PlayerCardState { UserId = HumanId, RoleCardId = RoleCardCatalog.Human },
                new PlayerCardState { UserId = ZombieId, RoleCardId = RoleCardCatalog.Infection }
            ]
        };

        foreach (var player in state.Players)
            player.ActionsUsedThisTurn = 0;

        return state;
    }

    private static void EnsureCardInHand(GameSessionState state, Guid playerId, CardDefinition card)
    {
        var hand = state.PlayerHands.First(h => h.UserId == playerId);
        if (card.EffectKey is "shoot" or "heal" or "shield")
        {
            if (hand.InventorySlot1 is null)
                hand.InventorySlot1 = card.Id;
            else if (hand.InventorySlot2 is null)
                hand.InventorySlot2 = card.Id;
            else
                hand.InventorySlot2 = card.Id;
        }
    }

    private static CardDefinition CardFor(BattleAction action) => action switch
    {
        BattleAction.Shoot => Shotgun,
        BattleAction.Heal => Heal,
        BattleAction.Shield => Shield,
        BattleAction.Infect => Infection,
        BattleAction.HumanRole => HumanRoleCard,
        _ => throw new InvalidOperationException()
    };

    private static string Label(BattleAction action) => action switch
    {
        BattleAction.Pass => "پاس",
        BattleAction.HumanRole => "انسان",
        BattleAction.Shield => "سپر",
        BattleAction.Shoot => "شات‌گان",
        BattleAction.Heal => "درمان",
        BattleAction.Infect => "مسمومیت",
        _ => action.ToString()
    };

    private static string RoleFa(PlayerRole role) => role switch
    {
        PlayerRole.Human => "انسان",
        PlayerRole.Zombie => "زامبی",
        PlayerRole.PowerZombie => "پاورزامبی",
        _ => role.ToString()
    };

    private static string ActorFa(bool isHuman) => isHuman ? "انسان" : "زامبی";

    private static string SlotFa(string slot) => slot switch
    {
        "H1" => "انسان (۱)",
        "H2" => "انسان (۲)",
        "Z1" => "زامبی (۱)",
        "Z2" => "زامبی (۲)",
        _ => slot
    };

    private sealed class SimpleRegistry : ICardRegistry
    {
        private readonly Dictionary<Guid, CardDefinition> _cards = new()
        {
            [HumanRoleCard.Id] = HumanRoleCard,
            [Shotgun.Id] = Shotgun,
            [Heal.Id] = Heal,
            [Shield.Id] = Shield,
            [Infection.Id] = Infection
        };

        public CardDefinition? GetById(Guid id) => _cards.GetValueOrDefault(id);
        public IReadOnlyList<CardDefinition> GetAll() => _cards.Values.ToList();
        public IReadOnlyList<CardDefinition> GetByType(CardType type) =>
            _cards.Values.Where(c => c.Type == type).ToList();
    }

    private sealed class NoOpRoleAssignment : IRoleAssignmentService
    {
        public void AssignRoles(GameSessionState state, int playerCount, RoleComposition? composition = null) { }
    }

    private sealed class NoOpDealing : ICardDealingService
    {
        public void InitializeHands(GameSessionState state) { }
        public void ReplenishInventory(GameSessionState state) { }
    }

    private sealed class NoOpCompletion : IMatchCompletionService
    {
        public Task CompleteMatchAsync(Match match, GameSessionState state, WinTeam winner, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
