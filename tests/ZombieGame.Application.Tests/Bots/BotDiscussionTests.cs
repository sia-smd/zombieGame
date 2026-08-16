namespace ZombieGame.Application.Tests.Bots;

using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.Bots.Discussion;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public class BotDiscussionTests
{
    [Fact]
    public void StructuredSuspectMessage_UpdatesListenerBelief_WithoutParsingText()
    {
        var listener = Guid.NewGuid();
        var speaker = Guid.NewGuid();
        var suspect = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (listener, PlayerRole.Human, true),
            (speaker, PlayerRole.Human, true),
            (suspect, PlayerRole.Zombie, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(1));
        BotReputationService.SetReputation(state, listener, speaker, 80);

        var before = BotBeliefService.GetBelief(state, listener, suspect);
        var message = new StructuredDiscussionMessage
        {
            SpeakerUserId = speaker,
            MessageType = DiscussionMessageType.SuspectPlayer,
            TargetUserId = suspect,
            DayNumber = 1
        };

        BotDiscussionProcessor.OnStructuredMessage(state, message);

        Assert.True(BotBeliefService.GetBelief(state, listener, suspect) > before);
    }

    [Fact]
    public void DisplayText_IsNotUsedByBeliefProcessor()
    {
        var listener = Guid.NewGuid();
        var speaker = Guid.NewGuid();
        var suspect = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (listener, PlayerRole.Human, true),
            (speaker, PlayerRole.Human, true),
            (suspect, PlayerRole.Human, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(2));
        state.BotCognition[listener].Personality.Trust = 80;
        BotReputationService.SetReputation(state, listener, speaker, 90);

        var misleadingText = DiscussionMessageFormatter.Format("Speaker", DiscussionMessageType.TrustPlayer, "Innocent");
        Assert.Contains("trusts", misleadingText, StringComparison.OrdinalIgnoreCase);

        var actualMessage = new StructuredDiscussionMessage
        {
            SpeakerUserId = speaker,
            MessageType = DiscussionMessageType.SuspectPlayer,
            TargetUserId = suspect,
            DayNumber = 1
        };

        var before = BotBeliefService.GetBelief(state, listener, suspect);
        BotDiscussionProcessor.OnStructuredMessage(state, actualMessage);
        var after = BotBeliefService.GetBelief(state, listener, suspect);

        Assert.True(after > before);
    }

    [Fact]
    public void LowReputationSpeaker_HasReducedBeliefImpact()
    {
        var listener = Guid.NewGuid();
        var trustedSpeaker = Guid.NewGuid();
        var distrustedSpeaker = Guid.NewGuid();
        var suspect = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (listener, PlayerRole.Human, true),
            (trustedSpeaker, PlayerRole.Human, true),
            (distrustedSpeaker, PlayerRole.Human, true),
            (suspect, PlayerRole.Zombie, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(3));
        BotReputationService.SetReputation(state, listener, trustedSpeaker, 90);
        BotReputationService.SetReputation(state, listener, distrustedSpeaker, 10);

        BotDiscussionProcessor.OnStructuredMessage(state, new StructuredDiscussionMessage
        {
            SpeakerUserId = trustedSpeaker,
            MessageType = DiscussionMessageType.SuspectPlayer,
            TargetUserId = suspect,
            DayNumber = 1
        });
        var afterTrusted = BotBeliefService.GetBelief(state, listener, suspect);

        BotBeliefService.SetBelief(state, listener, suspect, 25);
        BotDiscussionProcessor.OnStructuredMessage(state, new StructuredDiscussionMessage
        {
            SpeakerUserId = distrustedSpeaker,
            MessageType = DiscussionMessageType.SuspectPlayer,
            TargetUserId = suspect,
            DayNumber = 1
        });
        var afterDistrusted = BotBeliefService.GetBelief(state, listener, suspect);

        Assert.True(afterTrusted > afterDistrusted);
    }

    [Fact]
    public void QuietPersonality_SpeaksLessOftenThanTalkative()
    {
        var quietBot = Guid.NewGuid();
        var talkativeBot = Guid.NewGuid();
        var other = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (quietBot, PlayerRole.Human, true),
            (talkativeBot, PlayerRole.Human, true),
            (other, PlayerRole.Human, true));
        state.Players[0].IsBot = true;
        state.Players[1].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(4));
        BotBeliefService.SetBelief(state, quietBot, other, 70);
        BotBeliefService.SetBelief(state, talkativeBot, other, 70);
        state.BotCognition[quietBot].Personality.CommunicationStyle = CommunicationStyle.Quiet;
        state.BotCognition[talkativeBot].Personality.CommunicationStyle = CommunicationStyle.Talkative;
        state.BotCognition[quietBot].Personality.Confidence = 80;
        state.BotCognition[talkativeBot].Personality.Confidence = 80;

        var engine = new BotDiscussionEngine();
        var quietSpeaks = 0;
        var talkativeSpeaks = 0;
        for (var i = 0; i < 50; i++)
        {
            if (engine.ShouldSpeak(state, quietBot, new Random(i)))
                quietSpeaks++;
            if (engine.ShouldSpeak(state, talkativeBot, new Random(i + 500)))
                talkativeSpeaks++;
        }

        Assert.True(talkativeSpeaks > quietSpeaks);
    }

    [Fact]
    public void Announcements_DoNotRevealHiddenRoleCounts()
    {
        var state = GameTestBuilder.CreateSession(
            (Guid.NewGuid(), PlayerRole.Human, true),
            (Guid.NewGuid(), PlayerRole.Zombie, true),
            (Guid.NewGuid(), PlayerRole.PowerZombie, true));
        state.PublicInfectionEventCount = 2;
        state.DaysSinceLastElimination = 5;

        var announcements = DiscussionAnnouncementService.Build(state);
        var text = string.Join(' ', announcements.Select(DiscussionMessageFormatter.FormatAnnouncement));

        Assert.Contains("3 players remaining", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("zombie", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("human", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitializeBots_AssignsCommunicationStyle()
    {
        var botId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((botId, PlayerRole.Human, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(42));

        Assert.Contains(
            state.BotCognition[botId].Personality.CommunicationStyle,
            new[] { CommunicationStyle.Talkative, CommunicationStyle.Balanced, CommunicationStyle.Quiet });
    }
}
