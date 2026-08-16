namespace ZombieGame.Domain.Enums;

/// <summary>Room lifecycle phases for the multiplayer card game (Room Flow V2).</summary>
public enum RoomPhase
{
    Lobby = 0,
    DayStart = 1,
    OpponentSelection = 2,
    CardBattle = 3,
    BattleResult = 4,
    Discussion = 5,
    Voting = 6,
    VoteResult = 7,
    Finished = 8,
    /// <summary>Brief wait after pairing while battle rooms are prepared.</summary>
    BattlePreparation = 9,
    /// <summary>Public day outcomes before discussion.</summary>
    DaySummary = 10
}
