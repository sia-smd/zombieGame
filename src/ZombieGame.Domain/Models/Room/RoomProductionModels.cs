namespace ZombieGame.Domain.Models.Room;

using ZombieGame.Domain.Enums;

/// <summary>Independent 1v1 battle aggregate. BattleId == BattlePair.PairId.</summary>
public class Battle
{
    public Guid BattleId { get; set; }
    public Guid MatchId { get; set; }
    public int DayNumber { get; set; }
    public Guid PlayerA { get; set; }
    public Guid PlayerB { get; set; }
    public BattleAggregateStatus Status { get; set; } = BattleAggregateStatus.Pending;
    public DateTime? BattleEndsAt { get; set; }
    public bool PlayerAFinished { get; set; }
    public bool PlayerBFinished { get; set; }
    public BattlePublicAction PlayerAPublicAction { get; set; } = BattlePublicAction.None;
    public BattlePublicAction PlayerBPublicAction { get; set; } = BattlePublicAction.None;
    public List<BattleCardPlay> CardsPlayed { get; set; } = new();
    public BattlePublicAction? ResultPlayerA => PlayerAPublicAction != BattlePublicAction.None ? PlayerAPublicAction : null;
    public BattlePublicAction? ResultPlayerB => PlayerBPublicAction != BattlePublicAction.None ? PlayerBPublicAction : null;

    public bool IsMember(Guid userId) => userId == PlayerA || userId == PlayerB;

    public bool IsFinished => Status == BattleAggregateStatus.Finished
        || (PlayerAFinished && PlayerBFinished);
}

public class BattleCardPlay
{
    public Guid PlayerId { get; set; }
    public Guid CardId { get; set; }
    /// <summary>Inventory slot (0–3) when the same card id appears in multiple slots.</summary>
    public int? InventorySlotIndex { get; set; }
    public Guid? TargetUserId { get; set; }
    public DateTime PlayedAt { get; set; }
    /// <summary>True when resolution skipped this play (e.g. Heal preempted the attacker's Infection).</summary>
    public bool Skipped { get; set; }
}

public class PlayerActiveMatch
{
    public Guid PlayerId { get; set; }
    public Guid MatchId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public Guid? CurrentBattleId { get; set; }
    public RoomPhase CurrentRoomPhase { get; set; }
    public int DayNumber { get; set; }
    public bool IsAlive { get; set; } = true;
    public PlayerRole Role { get; set; } = PlayerRole.Unknown;
    public MatchConnectionRole ConnectionRole { get; set; } = MatchConnectionRole.Player;
    public DateTime LastSeen { get; set; }
    public string? RoomName { get; set; }
}

public class MatchEventEntry
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public MatchEventType EventType { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public int SequenceNumber { get; set; }
}

public class RoomSnapshot
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public SnapshotKind Kind { get; set; }
    public int DayNumber { get; set; }
    public RoomPhase Phase { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}

public class MatchStatistics
{
    public int TotalBattles { get; set; }
    public int TotalInfections { get; set; }
    public int TotalKills { get; set; }
    public int TotalEliminations { get; set; }
    public int TotalVotes { get; set; }
    public int TotalCardsPlayed { get; set; }
    public Dictionary<Guid, int> ActionsByPlayer { get; set; } = new();
    public DateTime? MatchStartedAt { get; set; }
}

public class MatchSummary
{
    public Guid MatchId { get; set; }
    public WinTeam WinningTeam { get; set; }
    public int TotalDays { get; set; }
    public int Battles { get; set; }
    public int Infections { get; set; }
    public int Kills { get; set; }
    public int Eliminations { get; set; }
    public int Votes { get; set; }
    public int CardsPlayed { get; set; }
    public Guid? MostActivePlayerId { get; set; }
    public Guid? MvpPlayerId { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime FinishedAt { get; set; }
}
