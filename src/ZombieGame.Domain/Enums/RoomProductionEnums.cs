namespace ZombieGame.Domain.Enums;

public enum MatchConnectionRole
{
    Player = 0,
    Spectator = 1,
    Admin = 2
}

public enum SnapshotKind
{
    EndOfDay = 0,
    EndOfBattle = 1,
    EndOfDiscussion = 2,
    EndOfVoting = 3,
    EndOfMatch = 4
}

public enum MatchEventType
{
    MatchStarted = 0,
    DayStarted = 1,
    InvitationSent = 2,
    InvitationAccepted = 3,
    InvitationsCancelled = 4,
    BattleStarted = 5,
    BattleFinished = 6,
    PlayerInfected = 7,
    PlayerEliminated = 8,
    DiscussionStarted = 9,
    VotingStarted = 10,
    VoteFinished = 11,
    PhaseReady = 12,
    PlayerDisconnected = 13,
    PlayerReconnected = 14,
    GameFinished = 15,
    ChatExported = 16,
    PlayerKilled = 17
}

public enum BattleAggregateStatus
{
    Pending = 0,
    InProgress = 1,
    Finished = 2
}
