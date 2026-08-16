namespace ZombieGame.Application.Bots.Discussion;

using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public static class DiscussionAnnouncementService
{
    public static IReadOnlyList<GlobalDiscussionAnnouncement> Build(GameSessionState state)
    {
        var alive = state.AlivePlayers.Count();
        var announcements = new List<GlobalDiscussionAnnouncement>
        {
            new()
            {
                Type = GlobalAnnouncementType.PlayersRemaining,
                DayNumber = state.TurnNumber,
                PlayersRemaining = alive
            },
            new()
            {
                Type = GlobalAnnouncementType.DangerLevel,
                DayNumber = state.TurnNumber,
                Danger = ComputeDanger(state)
            }
        };

        if (state.PublicInfectionEventCount > 0)
        {
            announcements.Add(new GlobalDiscussionAnnouncement
            {
                Type = GlobalAnnouncementType.InfectionSpreading,
                DayNumber = state.TurnNumber
            });
        }

        return announcements;
    }

    private static DangerLevel ComputeDanger(GameSessionState state)
    {
        if (state.DaysSinceLastElimination >= 8 || state.PublicInfectionEventCount >= 4)
            return DangerLevel.High;
        if (state.DaysSinceLastElimination >= 4 || state.PublicInfectionEventCount >= 2)
            return DangerLevel.Medium;
        return DangerLevel.Low;
    }
}

public static class BotDiscussionProcessor
{
    public static void OnAnnouncements(GameSessionState state, IEnumerable<GlobalDiscussionAnnouncement> announcements)
    {
        state.DiscussionAnnouncementsThisDay = announcements.ToList();

        foreach (var bot in state.Players.Where(p => p.IsBot))
        {
            foreach (var announcement in announcements)
            {
                if (announcement.Type != GlobalAnnouncementType.DangerLevel)
                    continue;

                if (announcement.Danger is not (DangerLevel.High or DangerLevel.Medium))
                    continue;

                foreach (var other in state.AlivePlayers.Where(p => p.UserId != bot.UserId))
                {
                    var bump = announcement.Danger == DangerLevel.High ? 3 : 1;
                    BotBeliefService.AdjustBelief(state, bot.UserId, other.UserId, bump);
                }
            }
        }
    }

    public static void OnStructuredMessage(GameSessionState state, StructuredDiscussionMessage message)
    {
        state.DiscussionMessagesThisDay.Add(message);

        foreach (var listener in state.Players.Where(p => p.IsBot && p.IsAlive && p.UserId != message.SpeakerUserId))
            ApplyMessageToListener(state, listener.UserId, message);
    }

    private static void ApplyMessageToListener(
        GameSessionState state,
        Guid listenerId,
        StructuredDiscussionMessage message)
    {
        if (message.TargetUserId is not Guid targetId || targetId == listenerId)
        {
            if (message.MessageType is DiscussionMessageType.NoEvidence or DiscussionMessageType.SelfDefense)
                return;
        }

        var personality = state.BotCognition.TryGetValue(listenerId, out var cognition)
            ? cognition.Personality
            : new BotPersonality();

        var reputation = BotReputationService.GetReputation(state, listenerId, message.SpeakerUserId);
        var trustFactor = reputation / 100.0 * (personality.Trust / 100.0);
        if (trustFactor < 0.15)
            return;

        var confidenceFactor = 0.5 + personality.Confidence / 200.0;
        var delta = MessageBeliefDelta(message.MessageType) * trustFactor * confidenceFactor;

        if (message.TargetUserId is Guid subjectId && subjectId != listenerId)
        {
            if (delta > 0)
                BotBeliefService.AdjustBelief(state, listenerId, subjectId, (int)Math.Round(delta));
            else
                BotBeliefService.AdjustBelief(state, listenerId, subjectId, (int)Math.Round(delta));
        }

        if (message.MessageType is DiscussionMessageType.DistrustPlayer && message.TargetUserId is Guid distrusted)
            BotMemoryService.AddMemory(state, listenerId, distrusted, BotMemoryKind.RepeatedPass, 0.5);
    }

    private static double MessageBeliefDelta(DiscussionMessageType type) => type switch
    {
        DiscussionMessageType.SuspectPlayer => 6,
        DiscussionMessageType.DefendPlayer => -5,
        DiscussionMessageType.ReportPass => 4,
        DiscussionMessageType.ReportAttack => 5,
        DiscussionMessageType.ReportShield => -2,
        DiscussionMessageType.ReportHeal => -4,
        DiscussionMessageType.ReportInfection => 12,
        DiscussionMessageType.TrustPlayer => -4,
        DiscussionMessageType.DistrustPlayer => 5,
        DiscussionMessageType.AskAboutPlayer => 2,
        DiscussionMessageType.FollowGroup => 4,
        _ => 0
    };
}
