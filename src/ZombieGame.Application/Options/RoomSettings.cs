namespace ZombieGame.Application.Options;

using ZombieGame.Domain.Enums;

public class RoomSettings
{
    public const string SectionName = "RoomSettings";

    /// <summary>Brief Day Start display before opponent selection (Room Flow V2).</summary>
    public int DayStartSeconds { get; set; } = 5;

    public int OpponentSelectionSeconds { get; set; } = 60;

    /// <summary>How long an invited player has to accept or reject a battle invitation.</summary>
    public int InvitationTimeoutSeconds { get; set; } = 5;

    public int CardBattleSeconds { get; set; } = 60;
    public int DiscussionPhaseSeconds { get; set; } = 60;
    public int VotingPhaseSeconds { get; set; } = 30;
    public int BattleResultDisplaySeconds { get; set; } = 10;
    public int VoteResultDisplaySeconds { get; set; } = 10;

    /// <summary>Preparing Battles… countdown before CardBattle (Room Flow V2).</summary>
    public int BattlePreparationSeconds { get; set; } = 3;

    /// <summary>Day summary display after battle results (Room Flow V2).</summary>
    public int DaySummarySeconds { get; set; } = 10;

    /// <summary>Grace window before a disconnected player is auto-passed in battle (Room Flow V2).</summary>
    public int BattleDisconnectGraceSeconds { get; set; } = 10;

    /// <summary>Consecutive idle days before a player is removed for being AFK. 0 disables.</summary>
    public int AfkDayLimit { get; set; } = 3;

    public UnmatchedPlayerRule UnmatchedPlayerRule { get; set; } = UnmatchedPlayerRule.Skip;
    public int MaxVotesPerPlayer { get; set; } = 1;
    public bool ExportDiscussionOnRoomFinish { get; set; } = true;
    public int RoomLoopIntervalMs { get; set; } = 500;

    /// <summary>Redis/in-memory lock lease. Must outlast a single command including DayStart fee collection.</summary>
    public int LockTtlSeconds { get; set; } = 30;

    /// <summary>
    /// How long a writer waits to acquire the room lock before failing.
    /// Lets two players in the same battle serialize instead of last-write-wins.
    /// </summary>
    public int LockWaitMilliseconds { get; set; } = 2000;

    public bool RoomBotsEnabled { get; set; } = true;
    public int BotInviteMinDelaySeconds { get; set; } = 1;
    public int BotInviteMaxDelaySeconds { get; set; } = 4;
    public int BotAcceptMinDelaySeconds { get; set; } = 1;
    public int BotAcceptMaxDelaySeconds { get; set; } = 5;
    public int BotBattleMinDelaySeconds { get; set; } = 2;
    public int BotBattleMaxDelaySeconds { get; set; } = 8;
    public int BotChatMinDelaySeconds { get; set; } = 1;
    public int BotChatMaxDelaySeconds { get; set; } = 4;
    public int BotVoteMinDelaySeconds { get; set; } = 1;
    public int BotVoteMaxDelaySeconds { get; set; } = 5;
    public int BotReadyMinDelaySeconds { get; set; } = 1;
    public int BotReadyMaxDelaySeconds { get; set; } = 3;
}
