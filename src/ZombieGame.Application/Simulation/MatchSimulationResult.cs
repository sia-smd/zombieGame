namespace ZombieGame.Application.Simulation;

using ZombieGame.Domain.Enums;

public sealed class BalanceStatistics
{
    public int TotalMatches { get; set; }
    public int HumanWins { get; set; }
    public int ZombieWins { get; set; }
    public int Stalemates { get; set; }
    public double HumanWinRate { get; set; }
    public double ZombieWinRate { get; set; }
    public double StalemateRate { get; set; }
    public double AverageTurns { get; set; }
    public double AverageFriendlyFirePerMatch { get; set; }
    public double AverageEliminationsByVote { get; set; }
    public double AverageCardPlaysPerMatch { get; set; }
    public Dictionary<string, int> WinsByFinalDayEvent { get; set; } = new();
    public Dictionary<string, int> WinsByStartingDayEvent { get; set; } = new();
    public Dictionary<string, int> CardPlaysByEffect { get; set; } = new();
    public Dictionary<string, double> WinRateWhenRolePresentAtStart { get; set; } = new();
    public Dictionary<string, int> DayEventOccurrences { get; set; } = new();
    public Dictionary<int, int> TurnDistribution { get; set; } = new();
    public int MinTurns { get; set; }
    public int MaxTurns { get; set; }
}

public sealed class DetailedMatchReplayResult
{
    public DetailedMatchReplay Replay { get; set; } = new();
    public string TextReport { get; set; } = string.Empty;
    public SingleMatchOutcome Outcome { get; set; } = new();
}

public sealed class MatchSimulationResult
{
    public MatchSimulationOptions Options { get; set; } = new();
    public BalanceStatistics Balance { get; set; } = new();
    public PassUsageTelemetry PassTelemetry { get; set; } = new();
    public InfectionTelemetry InfectionTelemetry { get; set; } = new();
    public VotingTelemetry VotingTelemetry { get; set; } = new();
    public TimeSpan Elapsed { get; set; }
    public double MatchesPerSecond { get; set; }
}

public sealed class SingleMatchOutcome
{
    public WinTeam? Winner { get; init; }
    public bool IsStalemate { get; init; }
    public int Turns { get; init; }
    public int FriendlyFireCount { get; init; }
    public int VoteEliminations { get; init; }
    public int TotalCardPlays { get; init; }
    public DayEventType StartingDayEvent { get; init; }
    public DayEventType FinalDayEvent { get; init; }
    public Dictionary<PlayerRole, bool> StartingRolesPresent { get; init; } = new();
    public Dictionary<string, int> CardPlaysByEffect { get; init; } = new();
    public RolePassCounts PassCounts { get; init; } = new();
    public RoleCardPlayCounts CardPlayCounts { get; init; } = new();
    public Dictionary<string, int> DayEventCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int HumanPlayersAtStart { get; init; }
    public int ZombiePlayersAtStart { get; init; }
    public int PowerZombiePlayersAtStart { get; init; }
    public InfectionMatchCounts InfectionCounts { get; init; } = new();
    public VotingMatchCounts VotingCounts { get; init; } = new();
    public bool HumanWon => Winner == WinTeam.Humans;
}
