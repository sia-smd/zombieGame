namespace ZombieGame.Application.Bots.Discussion;

using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public sealed class BotDiscussionEngine
{
    public bool ShouldSpeak(GameSessionState state, Guid botUserId, Random random)
    {
        var bot = state.GetPlayer(botUserId);
        if (bot is null || !bot.IsAlive || !bot.IsBot)
            return false;

        BotMemoryService.EnsureCognition(state, botUserId);
        var cognition = state.BotCognition[botUserId];
        var style = cognition.Personality.CommunicationStyle;

        if (!CanSpeakMore(cognition, style))
            return false;

        var evidenceScore = ComputeEvidenceScore(state, botUserId);
        if (evidenceScore < MinimumEvidence(style) && random.NextDouble() > SpeakChance(style) * 0.3)
            return false;

        var speakChance = SpeakChance(style) + evidenceScore / 200.0;
        speakChance += cognition.Personality.Confidence / 300.0;
        speakChance += cognition.Personality.TalkManipulation / 250.0;
        if (evidenceScore >= 40)
            speakChance += 0.2;

        return random.NextDouble() < Math.Clamp(speakChance, 0.02, 0.85);
    }

    public StructuredDiscussionMessage? DecideMessage(GameSessionState state, Guid botUserId, Random random)
    {
        var bot = state.GetPlayer(botUserId);
        if (bot is null || !bot.IsAlive)
            return null;

        BotMemoryService.EnsureCognition(state, botUserId);
        var cognition = state.BotCognition[botUserId];

        if (bot.IsInfectedTeam)
        {
            var manipulation = cognition.Personality.TalkManipulation;
            var allyDefense = TryDefendAlly(state, botUserId, random, cognition);
            if (allyDefense is not null)
                return allyDefense;

            var silentChance = Math.Clamp(0.35 - manipulation / 250.0, 0.12, 0.35);
            var honestChance = Math.Clamp(0.20 + manipulation / 400.0, 0.15, 0.30);
            var roll = random.NextDouble();
            if (roll < silentChance)
                return null;
            if (roll < silentChance + honestChance)
                return BuildHonestMessage(state, botUserId, random);
            return BuildMisleadingMessage(state, botUserId, random);
        }

        return BuildHonestMessage(state, botUserId, random);
    }

    public void RecordSpoke(GameSessionState state, Guid botUserId)
    {
        BotMemoryService.EnsureCognition(state, botUserId);
        state.BotCognition[botUserId].DiscussionMessagesThisMatch++;
    }

    private static StructuredDiscussionMessage? BuildHonestMessage(
        GameSessionState state,
        Guid botUserId,
        Random random)
    {
        var cognition = state.BotCognition[botUserId];
        var candidates = state.AlivePlayers.Where(p => p.UserId != botUserId).ToList();
        if (candidates.Count == 0)
            return Create(botUserId, DiscussionMessageType.NoEvidence, null, state.TurnNumber);

        var infectionMemory = cognition.Memories
            .Where(m => m.Kind == BotMemoryKind.ZombieRevealWitnessed)
            .OrderByDescending(m => m.Weight)
            .FirstOrDefault();
        if (infectionMemory is not null)
        {
            return Create(
                botUserId,
                DiscussionMessageType.ReportInfection,
                infectionMemory.SubjectUserId,
                state.TurnNumber);
        }

        var votedAgainstMe = cognition.Memories
            .Where(m => m.Kind == BotMemoryKind.VotedAgainstMe && m.IsPersonal)
            .OrderByDescending(m => m.Weight)
            .FirstOrDefault();
        if (votedAgainstMe is not null && random.NextDouble() < 0.6)
        {
            return Create(
                botUserId,
                DiscussionMessageType.SelfDefense,
                botUserId,
                state.TurnNumber);
        }

        var topSuspect = candidates
            .OrderByDescending(p => BotBeliefService.GetBelief(state, botUserId, p.UserId))
            .First();
        var topBelief = BotBeliefService.GetBelief(state, botUserId, topSuspect.UserId);
        if (topBelief >= 55)
        {
            return Create(
                botUserId,
                DiscussionMessageType.SuspectPlayer,
                topSuspect.UserId,
                state.TurnNumber);
        }

        var passMemory = cognition.Memories
            .Where(m => m.Kind == BotMemoryKind.RepeatedPass)
            .OrderByDescending(m => m.Weight)
            .FirstOrDefault();
        if (passMemory is not null && passMemory.Weight >= 2)
        {
            return Create(
                botUserId,
                DiscussionMessageType.ReportPass,
                passMemory.SubjectUserId,
                state.TurnNumber);
        }

        var trusted = candidates
            .OrderBy(p => BotBeliefService.GetBelief(state, botUserId, p.UserId))
            .FirstOrDefault();
        if (trusted is not null &&
            BotBeliefService.GetBelief(state, botUserId, trusted.UserId) <= 20 &&
            random.NextDouble() < 0.4)
        {
            return Create(botUserId, DiscussionMessageType.TrustPlayer, trusted.UserId, state.TurnNumber);
        }

        if (random.NextDouble() < 0.15)
            return Create(botUserId, DiscussionMessageType.NoEvidence, null, state.TurnNumber);

        return null;
    }

    private static StructuredDiscussionMessage? TryDefendAlly(
        GameSessionState state,
        Guid botUserId,
        Random random,
        BotCognitionState cognition)
    {
        if (cognition.Personality.TalkManipulation < 25)
            return null;

        var ally = state.AlivePlayers
            .Where(p => p.UserId != botUserId)
            .Where(p => IsAllyFromBotPerspective(state, botUserId, p.UserId))
            .FirstOrDefault();
        if (ally is null)
            return null;

        var defendChance = cognition.Personality.TalkManipulation / 130.0;
        if (random.NextDouble() >= defendChance)
            return null;

        return Create(botUserId, DiscussionMessageType.TrustPlayer, ally.UserId, state.TurnNumber);
    }

    private static StructuredDiscussionMessage? BuildMisleadingMessage(
        GameSessionState state,
        Guid botUserId,
        Random random)
    {
        var candidates = state.AlivePlayers
            .Where(p => p.UserId != botUserId)
            .Where(p => !IsAllyFromBotPerspective(state, botUserId, p.UserId))
            .ToList();
        if (candidates.Count == 0)
            candidates = state.AlivePlayers.Where(p => p.UserId != botUserId).ToList();
        if (candidates.Count == 0)
            return Create(botUserId, DiscussionMessageType.NoEvidence, null, state.TurnNumber);

        var target = candidates[random.Next(candidates.Count)];
        return random.NextDouble() < 0.6
            ? Create(botUserId, DiscussionMessageType.SuspectPlayer, target.UserId, state.TurnNumber)
            : Create(botUserId, DiscussionMessageType.DistrustPlayer, target.UserId, state.TurnNumber);
    }

    private static bool IsAllyFromBotPerspective(GameSessionState state, Guid botUserId, Guid targetId)
    {
        if (!state.BotCognition.TryGetValue(botUserId, out var cognition))
            return false;

        return cognition.PubliclyKnownInfected.Contains(targetId) &&
               cognition.ObservedInfectors.Contains(targetId);
    }

    private static double ComputeEvidenceScore(GameSessionState state, Guid botUserId)
    {
        if (!state.BotCognition.TryGetValue(botUserId, out var cognition))
            return 0;

        var maxBelief = state.AlivePlayers
            .Where(p => p.UserId != botUserId)
            .Select(p => BotBeliefService.GetBelief(state, botUserId, p.UserId))
            .DefaultIfEmpty(0)
            .Max();

        var memoryWeight = cognition.Memories.Sum(m => m.Weight);
        var personalEvents = cognition.Memories.Count(m => m.IsPersonal) * 5;
        var witnessedInfection = cognition.Memories.Any(m => m.Kind == BotMemoryKind.ZombieRevealWitnessed) ? 25 : 0;
        var accused = cognition.Memories.Any(m => m.Kind == BotMemoryKind.VotedAgainstMe) ? 15 : 0;

        return maxBelief * 0.4 + memoryWeight * 2 + personalEvents + witnessedInfection + accused;
    }

    private static bool CanSpeakMore(BotCognitionState cognition, CommunicationStyle style)
    {
        var bonus = cognition.Personality.TalkManipulation >= 60 ? 1 : 0;
        return style switch
        {
            CommunicationStyle.Talkative => cognition.DiscussionMessagesThisMatch < 6 + bonus,
            CommunicationStyle.Balanced => cognition.DiscussionMessagesThisMatch < 3 + bonus,
            CommunicationStyle.Quiet => cognition.DiscussionMessagesThisMatch < 1 + bonus,
            _ => cognition.DiscussionMessagesThisMatch < 3 + bonus
        };
    }

    private static double SpeakChance(CommunicationStyle style) => style switch
    {
        CommunicationStyle.Talkative => 0.45,
        CommunicationStyle.Balanced => 0.22,
        CommunicationStyle.Quiet => 0.08,
        _ => 0.22
    };

    private static double MinimumEvidence(CommunicationStyle style) => style switch
    {
        CommunicationStyle.Talkative => 10,
        CommunicationStyle.Balanced => 20,
        CommunicationStyle.Quiet => 35,
        _ => 20
    };

    private static StructuredDiscussionMessage Create(
        Guid speakerId,
        DiscussionMessageType type,
        Guid? targetId,
        int day) =>
        new()
        {
            SpeakerUserId = speakerId,
            MessageType = type,
            TargetUserId = targetId,
            DayNumber = day
        };
}
