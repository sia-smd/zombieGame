namespace ZombieGame.Domain.Models;

using ZombieGame.Domain.Enums;

public class GameSessionState
{
    public Guid MatchId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public GamePhase CurrentPhase { get; set; } = GamePhase.Lobby;
    public int TurnNumber { get; set; }
    public DateTime? PhaseEndsAt { get; set; }
    public DayEventType CurrentDayEvent { get; set; } = DayEventType.NormalDay;
    public WinTeam WinTeam { get; set; } = WinTeam.None;
    public int FriendlyFireCount { get; set; }
    public Dictionary<Guid, Guid> Votes { get; set; } = new();
    public Dictionary<Guid, Dictionary<Guid, double>> SuspicionScores { get; set; } = new();
    public Dictionary<Guid, PlayerDayActivity> PlayerDayActivities { get; set; } = new();
    public List<GamePlayerState> Players { get; set; } = new();
    public List<PlayerCardState> PlayerHands { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsFinished => WinTeam != WinTeam.None;

    public IEnumerable<GamePlayerState> AlivePlayers => Players.Where(p => p.IsAlive);

    public GamePlayerState? GetPlayer(Guid userId) =>
        Players.FirstOrDefault(p => p.UserId == userId);
}

public class GamePlayerState
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public PlayerRole Role { get; set; }
    public bool IsBot { get; set; }
    public bool IsAlive { get; set; } = true;
    public int SeatIndex { get; set; }
    public bool HasShield { get; set; }
    public int RemainingHealth { get; set; } = 1;
    public int ShotgunHitCount { get; set; }
    public int ActionsUsedThisTurn { get; set; }
    public int ActionsPerTurn { get; set; } = 2;
    public int RemainingActions => Math.Max(0, ActionsPerTurn - ActionsUsedThisTurn);
    public bool HasRevealedThisDay { get; set; }
    public bool InactiveForNextDealing { get; set; }
    public int PassesUsedThisDay { get; set; }

    public bool IsInfectedTeam => Role is PlayerRole.Zombie or PlayerRole.PowerZombie;
}

public class PlayerDayActivity
{
    public int PassCount { get; set; }
    public int UsefulActionCount { get; set; }
}

public class PlayerCardState
{
    public Guid UserId { get; set; }
    public List<Guid> CardIds { get; set; } = new();
    public HashSet<Guid> DisabledCardIds { get; set; } = new();
}

public class PendingAction
{
    public Guid UserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
}
