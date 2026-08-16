namespace ZombieGame.Domain.Models;

public enum CommunicationStyle
{
    Talkative,
    Balanced,
    Quiet
}

public enum DiscussionMessageType
{
    SuspectPlayer,
    DefendPlayer,
    ReportPass,
    ReportAttack,
    ReportShield,
    ReportHeal,
    ReportInfection,
    TrustPlayer,
    DistrustPlayer,
    AskAboutPlayer,
    NoEvidence,
    SelfDefense,
    FollowGroup
}

public enum GlobalAnnouncementType
{
    PlayersRemaining,
    DangerLevel,
    InfectionSpreading
}

public enum DangerLevel
{
    Low,
    Medium,
    High
}

public sealed class StructuredDiscussionMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpeakerUserId { get; set; }
    public DiscussionMessageType MessageType { get; set; }
    public Guid? TargetUserId { get; set; }
    public int DayNumber { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public sealed class GlobalDiscussionAnnouncement
{
    public GlobalAnnouncementType Type { get; set; }
    public int DayNumber { get; set; }
    public int? PlayersRemaining { get; set; }
    public DangerLevel? Danger { get; set; }
}
