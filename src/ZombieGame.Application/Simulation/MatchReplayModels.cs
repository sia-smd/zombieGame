namespace ZombieGame.Application.Simulation;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public sealed class DetailedMatchReplay
{
    public Guid MatchId { get; set; }
    public int PlayerCount { get; set; }
    public int? RandomSeed { get; set; }
    public WinTeam? Winner { get; set; }
    public bool IsStalemate { get; set; }
    public int TotalDays { get; set; }
    public DayEventType StartingDayEvent { get; set; }
    public List<ReplayPlayerInfo> Players { get; set; } = [];
    public List<ReplayDayLog> Days { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}

public sealed class ReplayPlayerInfo
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SeatIndex { get; init; }
    public PlayerRole StartingRole { get; init; }
    public PlayerRole FinalRole { get; set; }
    public bool Survived { get; set; }
}

public sealed class ReplayPlayerHandSnapshot
{
    public string Name { get; init; } = string.Empty;
    public int SeatIndex { get; init; }
    public PlayerRole Role { get; set; }
    public bool IsAlive { get; set; } = true;
    public string? RoleCard { get; set; }
    public string? InventorySlot1 { get; set; }
    public string? InventorySlot2 { get; set; }
}

public sealed class ReplayDayLog
{
    public int DayNumber { get; init; }
    public DayEventType DayEvent { get; set; }
    public int AliveAtStart { get; set; }
    public string CardsDealtNote { get; set; } = string.Empty;
    public List<ReplayPlayerHandSnapshot> StartingHands { get; init; } = [];
    public List<ReplayBattleLog> Battles { get; init; } = [];
    public List<string> UnmatchedPlayers { get; init; } = [];
    public List<ReplayVoteLog> Votes { get; init; } = [];
    public List<ReplayDiscussionLog> DiscussionMessages { get; init; } = [];
    public List<string> DiscussionAnnouncements { get; init; } = [];
    public ReplayEliminationLog? Elimination { get; set; }
    public List<ReplayPlayerStatus> AliveAtEnd { get; init; } = [];
    public string? GameEndedNote { get; set; }
}

public sealed class ReplayBattleLog
{
    public string Player1 { get; init; } = string.Empty;
    public string Player2 { get; init; } = string.Empty;
    public ReplayBattleActionLog Player1Action { get; set; } = new();
    public ReplayBattleActionLog Player2Action { get; set; } = new();
}

public sealed class ReplayBattleActionLog
{
    public string Player { get; init; } = string.Empty;
    public string PublicAction { get; set; } = "Pass";
    public string? Detail { get; set; }
}

public sealed class ReplayVoteLog
{
    public string Voter { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
}

public sealed class ReplayDiscussionLog
{
    public string Speaker { get; init; } = string.Empty;
    public DiscussionMessageType MessageType { get; init; }
    public string? Target { get; init; }
    public string DisplayText { get; init; } = string.Empty;
}

public sealed class ReplayEliminationLog
{
    public string? EliminatedPlayer { get; set; }
    public PlayerRole? RevealedRole { get; set; }
    public bool NoElimination { get; set; }
    public Dictionary<string, int> VoteCounts { get; init; } = new();
}

public sealed class ReplayPlayerStatus
{
    public string Name { get; init; } = string.Empty;
    public PlayerRole Role { get; set; }
    public bool IsAlive { get; set; }
    public int HandSize { get; set; }
}

internal sealed class MatchReplayRecorder
{
    public DetailedMatchReplay Report { get; } = new();
    private ReplayDayLog? _currentDay;
    private ReplayBattleLog? _currentBattle;

    public void Init(Guid matchId, int playerCount, int? seed)
    {
        Report.MatchId = matchId;
        Report.PlayerCount = playerCount;
        Report.RandomSeed = seed;
    }

    public void RecordRoster(IEnumerable<(Guid Id, string Name, int Seat, PlayerRole Role)> players)
    {
        foreach (var p in players)
        {
            Report.Players.Add(new ReplayPlayerInfo
            {
                UserId = p.Id,
                Name = p.Name,
                SeatIndex = p.Seat,
                StartingRole = p.Role,
                FinalRole = p.Role,
                Survived = true
            });
        }
    }

    public void BeginDay(int dayNumber, DayEventType dayEvent, int aliveCount, string cardsNote)
    {
        _currentDay = new ReplayDayLog
        {
            DayNumber = dayNumber,
            DayEvent = dayEvent,
            AliveAtStart = aliveCount,
            CardsDealtNote = cardsNote
        };
        Report.Days.Add(_currentDay);
    }

    public void RecordStartingHands(IEnumerable<ReplayPlayerHandSnapshot> hands)
    {
        EnsureDay().StartingHands.AddRange(hands);
    }

    public void BeginBattle(string player1, string player2)
    {
        EnsureDay();
        _currentBattle = new ReplayBattleLog
        {
            Player1 = player1,
            Player2 = player2,
            Player1Action = new ReplayBattleActionLog { Player = player1 },
            Player2Action = new ReplayBattleActionLog { Player = player2 }
        };
        _currentDay!.Battles.Add(_currentBattle);
    }

    public void RecordBattlePass(string playerName)
    {
        EnsureBattle();
        var action = GetActionSlot(playerName);
        action.PublicAction = "Pass";
        action.Detail = "Pass";
    }

    public void RecordBattlePlay(string playerName, string cardName, string? targetName, string result)
    {
        EnsureBattle();
        var action = GetActionSlot(playerName);
        action.PublicAction = "Action";
        action.Detail = targetName is null
            ? $"{playerName} played {cardName} — {result}"
            : $"{playerName} played {cardName} → {targetName} — {result}";
    }

    public void RecordUnmatched(string playerName)
    {
        EnsureDay();
        _currentDay!.UnmatchedPlayers.Add($"{playerName} had no opponent (skipped battle)");
    }

    public void RecordVote(string voter, string target) =>
        EnsureDay().Votes.Add(new ReplayVoteLog { Voter = voter, Target = target });

    public void RecordDiscussion(string speaker, DiscussionMessageType type, string? target, string displayText) =>
        EnsureDay().DiscussionMessages.Add(new ReplayDiscussionLog
        {
            Speaker = speaker,
            MessageType = type,
            Target = target,
            DisplayText = displayText
        });

    public void RecordDiscussionAnnouncement(string text) =>
        EnsureDay().DiscussionAnnouncements.Add(text);

    public void RecordElimination(string? eliminatedName, PlayerRole? role, bool none, Dictionary<string, int> voteCounts)
    {
        EnsureDay().Elimination = new ReplayEliminationLog
        {
            EliminatedPlayer = eliminatedName,
            RevealedRole = role,
            NoElimination = none,
            VoteCounts = voteCounts
        };
    }

    public void EndDay(IEnumerable<ReplayPlayerStatus> statuses)
    {
        EnsureDay();
        _currentDay!.AliveAtEnd.AddRange(statuses);
        _currentBattle = null;
    }

    public void RecordGameEnd(string note)
    {
        if (_currentDay is not null)
            _currentDay.GameEndedNote = note;
    }

    public void Finalize(WinTeam? winner, bool stalemate, DayEventType startingEvent)
    {
        Report.Winner = winner;
        Report.IsStalemate = stalemate;
        Report.TotalDays = Report.Days.Count;
        Report.StartingDayEvent = startingEvent;
        foreach (var p in Report.Players)
        {
            var final = statuses.FirstOrDefault(s => s.Name == p.Name);
            if (final is not null)
            {
                p.FinalRole = final.Role;
                p.Survived = final.IsAlive;
            }
        }
        Report.Summary = BuildSummaryText();
    }

    private List<ReplayPlayerStatus> statuses = [];

    public void SetFinalStatuses(IEnumerable<ReplayPlayerStatus> s) => statuses = s.ToList();

    private ReplayBattleActionLog GetActionSlot(string playerName)
    {
        EnsureBattle();
        if (_currentBattle!.Player1Action.Player == playerName)
            return _currentBattle.Player1Action;
        return _currentBattle.Player2Action;
    }

    private ReplayDayLog EnsureDay()
    {
        if (_currentDay is null)
            throw new InvalidOperationException("No active day in replay recorder.");
        return _currentDay;
    }

    private void EnsureBattle()
    {
        if (_currentBattle is null)
            throw new InvalidOperationException("No active battle in replay recorder.");
    }

    private string BuildSummaryText()
    {
        var winner = Report.IsStalemate ? "Stalemate" : Report.Winner?.ToString() ?? "Unknown";
        return $"Match {Report.MatchId:N} — {Report.PlayerCount} bots — {Report.TotalDays} day(s) — Winner: {winner}";
    }
}
