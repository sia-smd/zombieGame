namespace ZombieGame.Application.Options;

public class GameSettings
{
    public const string SectionName = "GameSettings";

    public int MatchmakingPlayerCount { get; set; } = 8;
    public int MaxMatchPlayers { get; set; } = 16;
    public int MatchEntryFeeCoins { get; set; } = 2;
    public int MatchWinRewardCoins { get; set; } = 4;
    public int StartingCoins { get; set; } = 10;
    public int DiscussionPhaseSeconds { get; set; } = 60;
    public int VotingPhaseSeconds { get; set; } = 60;
    public int InitialHandSize { get; set; } = 3;
    public int CardsDealtPerDay { get; set; } = 1;
    public int ActionsPerTurn { get; set; } = 2;
    /// <summary>Maximum Pass actions allowed per player per Day. 0 = unlimited.</summary>
    public int MaxPassActionsPerDay { get; set; } = 1;
    /// <summary>When true, Shield blocks PowerZombie infection (experimental balance mode).</summary>
    public bool ShieldBlocksPowerZombieInfection { get; set; }
    public bool FillWithBotsWhenUnderCapacity { get; set; } = true;
    public bool EnableGameLoop { get; set; } = true;
    public int GameLoopIntervalMs { get; set; } = 500;
}
