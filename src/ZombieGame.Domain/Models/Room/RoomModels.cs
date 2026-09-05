namespace ZombieGame.Domain.Models.Room;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public class RoomState
{
    public Guid MatchId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public string? RoomName { get; set; }
    public int MaxPlayers { get; set; } = 8;
    public bool FillWithBots { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? HostUserId { get; set; }
    public RoomPhase CurrentPhase { get; set; } = RoomPhase.Lobby;
    public int DayNumber { get; set; }
    public DateTime? PhaseEndsAt { get; set; }
    public DayEventType CurrentDayEvent { get; set; } = DayEventType.NormalDay;
    /// <summary>Preview shown during VoteResult before the day counter increments.</summary>
    public int? NextDayNumber { get; set; }
    public DayEventType? NextDayEvent { get; set; }
    public WinTeam WinTeam { get; set; } = WinTeam.None;
    public List<RoomPlayerState> Players { get; set; } = new();
    public List<BattleInvitation> PendingInvitations { get; set; } = new();
    public List<BattlePair> BattlePairs { get; set; } = new();
    public List<BattleSummary> CurrentDayBattleSummaries { get; set; } = new();
    public DaySummaryState DaySummary { get; set; } = new();
    /// <summary>Role snapshot at DayStart — used to detect new infections for DaySummary.</summary>
    public Dictionary<Guid, PlayerRole> RolesAtDayStart { get; set; } = new();
    public Dictionary<Guid, Guid> Votes { get; set; } = new();
    public Guid? LastEliminatedPlayerId { get; set; }
    public HashSet<Guid> PhaseReadyPlayers { get; set; } = new();
    public MatchStatistics Statistics { get; set; } = new();
    public DateTime? MatchStartedAt { get; set; }
    /// <summary>Monotonic snapshot id; incremented whenever the room is persisted.</summary>
    public int SnapshotVersion { get; set; }
    public bool IsFinished => WinTeam != WinTeam.None || CurrentPhase == RoomPhase.Finished;

    /// <summary>Embedded card/session data reused by battle logic.</summary>
    public GameSessionState Session { get; set; } = new();

    public IEnumerable<RoomPlayerState> AlivePlayers => Players.Where(p => p.IsAlive);
    public IEnumerable<RoomPlayerState> RestingPlayers => AlivePlayers.Where(p => p.IsResting);

    public RoomPlayerState? GetPlayer(Guid userId) =>
        Players.FirstOrDefault(p => p.UserId == userId);

    public bool IsPaired(Guid userId) =>
        BattlePairs.Any(p => p.Player1Id == userId || p.Player2Id == userId);

    /// <summary>True while the player is locked into an unanswered invitation, as sender or receiver.</summary>
    public bool HasPendingInvitation(Guid userId) =>
        PendingInvitations.Any(i =>
            i.Status == BattleInvitationStatus.Pending &&
            (i.FromUserId == userId || i.ToUserId == userId));
}

/// <summary>Public day outcomes shown during DaySummary (no cards/roles).</summary>
public class DaySummaryState
{
    public List<Guid> EliminatedPlayerIds { get; set; } = new();
    public List<Guid> NewlyInfectedPlayerIds { get; set; } = new();
    public List<Guid> RestingPlayerIds { get; set; } = new();
    public int AliveCount { get; set; }
}

public class RoomPlayerState
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ImageId { get; set; } = "avatar_default_01";
    public bool IsBot { get; set; }
    public bool IsAlive { get; set; } = true;
    public int SeatIndex { get; set; }
    public bool HasSentInvitationToday { get; set; }
    public bool IsReady { get; set; }
    /// <summary>Odd-one-out / unpaired for the day: no battle, still discusses and votes.</summary>
    public bool IsResting { get; set; }
    /// <summary>UTC time the player dropped the connection; null while connected.</summary>
    public DateTime? DisconnectedAt { get; set; }
    /// <summary>Any meaningful action (invite, respond, battle action, chat, vote) taken today.</summary>
    public bool HasActedToday { get; set; }
    /// <summary>Consecutive days without any action, used for AFK removal.</summary>
    public int InactiveDayCount { get; set; }
    /// <summary>UTC time when a bot should perform its next scheduled room action.</summary>
    public DateTime? BotNextActionAt { get; set; }

    public bool IsDisconnected => DisconnectedAt is not null;
}

public class BattleInvitation
{
    public Guid Id { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public DateTime SentAt { get; set; }
    /// <summary>UTC deadline after which the invitation is expired automatically.</summary>
    public DateTime? ExpiresAt { get; set; }
    public BattleInvitationStatus Status { get; set; } = BattleInvitationStatus.Pending;
    /// <summary>When set, a bot auto-accepts the invitation at or after this UTC time.</summary>
    public DateTime? BotRespondAt { get; set; }
}

public enum BattleInvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Expired = 3
}

public class BattlePair
{
    public Guid PairId { get; set; }
    public Guid Player1Id { get; set; }
    public Guid Player2Id { get; set; }
    public BattlePairStatus Status { get; set; } = BattlePairStatus.Pending;
    public BattlePublicAction Player1Summary { get; set; } = BattlePublicAction.None;
    public BattlePublicAction Player2Summary { get; set; } = BattlePublicAction.None;
    /// <summary>Private to battle members via RoomMeDto — never on public pair DTO.</summary>
    public List<Guid> Player1PlayedCardIds { get; set; } = new();
    public List<Guid> Player2PlayedCardIds { get; set; } = new();
    public PairBattleSession BattleSession { get; set; } = new();
    public DateTime? BattleEndsAt { get; set; }
}

public enum BattlePairStatus
{
    Pending = 0,
    InProgress = 1,
    Finished = 2
}

public class PairBattleSession
{
    public Guid PairId { get; set; }
    public bool Player1Finished { get; set; }
    public bool Player2Finished { get; set; }
    public BattlePublicAction Player1Action { get; set; } = BattlePublicAction.None;
    public BattlePublicAction Player2Action { get; set; } = BattlePublicAction.None;
}

public class BattleSummary
{
    public int DayNumber { get; set; }
    public Guid Player1Id { get; set; }
    public string Player1Name { get; set; } = string.Empty;
    public BattlePublicAction Player1Action { get; set; }
    public Guid Player2Id { get; set; }
    public string Player2Name { get; set; } = string.Empty;
    public BattlePublicAction Player2Action { get; set; }
}

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    /// <summary>Localized display text for humans. Bots never parse this.</summary>
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public DiscussionMessageType? MessageType { get; set; }
    public Guid? TargetUserId { get; set; }
    public bool IsStructured { get; set; }
}
