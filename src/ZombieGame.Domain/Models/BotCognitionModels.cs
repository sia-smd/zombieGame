namespace ZombieGame.Domain.Models;

public sealed class BotPersonality
{
    public int Aggression { get; set; }
    public int RiskTolerance { get; set; }
    public int Trust { get; set; }
    public int Patience { get; set; }
    public int Confidence { get; set; }
    public int TalkManipulation { get; set; }
    public CommunicationStyle CommunicationStyle { get; set; } = CommunicationStyle.Balanced;
}

public enum BotMemoryKind
{
    RepeatedPass,
    PublicAction,
    FriendlyFireWitnessed,
    ZombieRevealWitnessed,
    ShieldBlockWitnessed,
    HealWitnessed,
    ZombieKillWitnessed,
    PlayerEliminated,
    VotedAgainstMe,
    DefendedMe,
    SurvivedSuspiciously
}

public sealed class BotMemoryEntry
{
    public Guid SubjectUserId { get; set; }
    public BotMemoryKind Kind { get; set; }
    public double Weight { get; set; }
    public int DayRecorded { get; set; }
    public bool IsPersonal { get; set; }
}

public sealed class BotCognitionState
{
    public BotPersonality Personality { get; set; } = new();
    public List<BotMemoryEntry> Memories { get; set; } = [];
    /// <summary>Personal belief that a subject is a zombie (0–100). Not the real role.</summary>
    public Dictionary<Guid, int> ZombieBeliefs { get; set; } = new();
    /// <summary>Players this bot has publicly seen commit an infecting action.</summary>
    public HashSet<Guid> ObservedInfectors { get; set; } = [];
    /// <summary>Players publicly revealed as infected (witnessed infection on them or elimination reveal).</summary>
    public HashSet<Guid> PubliclyKnownInfected { get; set; } = [];
    /// <summary>How much this bot trusts another player's discussion (0–100, neutral 50).</summary>
    public Dictionary<Guid, int> SpeakerReputation { get; set; } = new();
    public int DiscussionMessagesThisMatch { get; set; }
}
