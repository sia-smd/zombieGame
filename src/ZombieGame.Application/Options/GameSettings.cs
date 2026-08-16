namespace ZombieGame.Application.Options;

public class GameSettings
{
    public const string SectionName = "GameSettings";

    /// <summary>Default max players when creating a room / matchmaking target size.</summary>
    public int MatchmakingPlayerCount { get; set; } = 8;
    public int MinRoomPlayers { get; set; } = 4;
    public int MaxRoomPlayers { get; set; } = 12;
    public int MaxMatchPlayers { get; set; } = 16;
    public int MatchEntryFeeCoins { get; set; } = 5;
    public int MatchWinRewardCoins { get; set; } = 4;
    public int StartingCoins { get; set; } = 10;
    public int DiscussionPhaseSeconds { get; set; } = 60;
    public int VotingPhaseSeconds { get; set; } = 30;
    public int ActionsPerTurn { get; set; } = 2;
    /// <summary>Maximum card plays or passes per battle/day turn (2 actions total).</summary>
    public int MaxPassActionsPerDay { get; set; } = 0;
    public CardDistributionSettings CardDistribution { get; set; } = new();
    public InventorySettings Inventory { get; set; } = new();
    public bool FillWithBotsWhenUnderCapacity { get; set; } = true;
    /// <summary>After this many seconds in queue without enough humans, create a match and fill with bots.</summary>
    public int MatchmakingBotFillTimeoutSeconds { get; set; } = 10;
    /// <summary>How long a waiting-room player invite stays valid.</summary>
    public int RoomInviteTtlSeconds { get; set; } = 60;
    public bool EnableGameLoop { get; set; } = true;
    public int GameLoopIntervalMs { get; set; } = 500;
}
