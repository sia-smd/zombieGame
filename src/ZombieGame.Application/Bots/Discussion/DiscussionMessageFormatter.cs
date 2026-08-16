namespace ZombieGame.Application.Bots.Discussion;

using ZombieGame.Domain.Models;

public static class DiscussionMessageFormatter
{
    public static string Format(string speakerName, DiscussionMessageType type, string? targetName = null) =>
        type switch
        {
            DiscussionMessageType.SuspectPlayer => $"{speakerName} suspects {targetName}.",
            DiscussionMessageType.DefendPlayer => $"{speakerName} defends {targetName}.",
            DiscussionMessageType.ReportPass => $"{speakerName} reports {targetName} passed repeatedly.",
            DiscussionMessageType.ReportAttack => $"{speakerName} reports {targetName} attacked someone.",
            DiscussionMessageType.ReportShield => $"{speakerName} reports {targetName} used a shield.",
            DiscussionMessageType.ReportHeal => $"{speakerName} reports {targetName} healed someone.",
            DiscussionMessageType.ReportInfection => $"{speakerName} reports {targetName} was involved in an infection.",
            DiscussionMessageType.TrustPlayer => $"{speakerName} trusts {targetName}.",
            DiscussionMessageType.DistrustPlayer => $"{speakerName} distrusts {targetName}.",
            DiscussionMessageType.AskAboutPlayer => $"{speakerName} asks about {targetName}.",
            DiscussionMessageType.NoEvidence => $"{speakerName} has no clear evidence yet.",
            DiscussionMessageType.SelfDefense => $"{speakerName} defends themselves.",
            DiscussionMessageType.FollowGroup => $"{speakerName} agrees with suspicion toward {targetName}.",
            _ => $"{speakerName} spoke."
        };

    public static string FormatAnnouncement(GlobalDiscussionAnnouncement announcement) =>
        announcement.Type switch
        {
            GlobalAnnouncementType.PlayersRemaining =>
                $"{announcement.PlayersRemaining} players remaining.",
            GlobalAnnouncementType.DangerLevel =>
                $"Danger level: {announcement.Danger}.",
            GlobalAnnouncementType.InfectionSpreading =>
                "The infection appears to be spreading.",
            _ => string.Empty
        };
}
