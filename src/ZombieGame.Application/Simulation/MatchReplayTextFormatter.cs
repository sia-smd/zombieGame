namespace ZombieGame.Application.Simulation;

using System.Text;
using ZombieGame.Domain.Enums;

public static class MatchReplayTextFormatter
{
    public static string Format(DetailedMatchReplay replay)
    {
        var sb = new StringBuilder();
        sb.AppendLine("══════════════════════════════════════════════════════════");
        sb.AppendLine("  ZOMBIE GAME — FULL MATCH REPLAY (ALL BOTS)");
        sb.AppendLine("══════════════════════════════════════════════════════════");
        sb.AppendLine(replay.Summary);
        sb.AppendLine($"Starting day event: {replay.StartingDayEvent}");
        sb.AppendLine();

        sb.AppendLine("── ROSTER (roles revealed for simulation report) ──");
        foreach (var p in replay.Players.OrderBy(p => p.SeatIndex))
        {
            sb.AppendLine($"  Seat {p.SeatIndex + 1,2}: {p.Name,-8} start={p.StartingRole,-12} final={p.FinalRole,-12} {(p.Survived ? "ALIVE" : "DEAD")}");
        }
        sb.AppendLine();

        foreach (var day in replay.Days)
        {
            sb.AppendLine($"━━━━━━━━━━━━━━ DAY {day.DayNumber} ━━━━━━━━━━━━━━");
            sb.AppendLine($"Event: {day.DayEvent} | Alive at start: {day.AliveAtStart}");
            sb.AppendLine($"Cards: {day.CardsDealtNote}");

            if (day.StartingHands.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  Hands at day start (before battles):");
                foreach (var hand in day.StartingHands.OrderBy(h => h.SeatIndex))
                {
                    var status = hand.IsAlive ? string.Empty : " [DEAD]";
                    sb.AppendLine($"    Seat {hand.SeatIndex + 1,2}: {hand.Name,-8} role={hand.Role,-12}{status}");
                    sb.AppendLine($"              Role:    {hand.RoleCard ?? "(none)"}");
                    sb.AppendLine($"              Slot 1:  {hand.InventorySlot1 ?? "(empty)"}");
                    sb.AppendLine($"              Slot 2:  {hand.InventorySlot2 ?? "(empty)"}");
                }
            }

            sb.AppendLine();

            if (day.Battles.Count > 0)
            {
                sb.AppendLine("  Battles (1v1):");
                var battleNum = 1;
                foreach (var b in day.Battles)
                {
                    sb.AppendLine($"  [{battleNum}] {b.Player1} vs {b.Player2}");
                    WriteAction(sb, "      ", b.Player1Action);
                    WriteAction(sb, "      ", b.Player2Action);
                    battleNum++;
                }
            }
            else
            {
                sb.AppendLine("  Battles: none");
            }

            foreach (var u in day.UnmatchedPlayers)
                sb.AppendLine($"  ⚠ {u}");

            sb.AppendLine();
            if (day.DiscussionAnnouncements.Count > 0)
            {
                sb.AppendLine("  Discussion announcements:");
                foreach (var a in day.DiscussionAnnouncements)
                    sb.AppendLine($"    • {a}");
            }

            if (day.DiscussionMessages.Count > 0)
            {
                sb.AppendLine("  Discussion:");
                foreach (var m in day.DiscussionMessages)
                    sb.AppendLine($"    [{m.MessageType}] {m.DisplayText}");
            }
            else if (day.DiscussionAnnouncements.Count == 0)
            {
                sb.AppendLine("  Discussion: (no messages)");
            }

            sb.AppendLine();

            if (day.Votes.Count > 0)
            {
                sb.AppendLine("  Votes (secret → revealed in report):");
                foreach (var v in day.Votes)
                    sb.AppendLine($"    {v.Voter} → {v.Target}");
            }

            if (day.Elimination is not null)
            {
                sb.AppendLine();
                if (day.Elimination.NoElimination)
                {
                    sb.AppendLine("  Vote result: No elimination (no majority)");
                }
                else
                {
                    sb.AppendLine($"  Vote result: {day.Elimination.EliminatedPlayer} ELIMINATED (role: {day.Elimination.RevealedRole})");
                }

                if (day.Elimination.VoteCounts.Count > 0)
                {
                    sb.AppendLine("  Vote counts:");
                    foreach (var (name, count) in day.Elimination.VoteCounts.OrderByDescending(v => v.Value))
                        sb.AppendLine($"    {name}: {count}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("  Alive at end of day:");
            foreach (var s in day.AliveAtEnd)
            {
                sb.AppendLine($"    {s.Name,-8} role={s.Role,-12} hand={s.HandSize} card(s) {(s.IsAlive ? "" : "[DEAD]")}");
            }

            if (!string.IsNullOrWhiteSpace(day.GameEndedNote))
            {
                sb.AppendLine();
                sb.AppendLine($"  ★ {day.GameEndedNote}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("══════════════════════════════════════════════════════════");
        var winner = replay.IsStalemate ? "Stalemate (max days reached)" : replay.Winner?.ToString() ?? "?";
        sb.AppendLine($"FINAL RESULT: {winner}");
        sb.AppendLine("══════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    private static void WriteAction(StringBuilder sb, string indent, ReplayBattleActionLog action)
    {
        var pub = action.PublicAction == "Action" ? "ACTION" : "PASS";
        sb.AppendLine($"{indent}{action.Player}: {pub}");
        if (!string.IsNullOrWhiteSpace(action.Detail) && action.PublicAction == "Action")
            sb.AppendLine($"{indent}  └ {action.Detail}");
    }
}
