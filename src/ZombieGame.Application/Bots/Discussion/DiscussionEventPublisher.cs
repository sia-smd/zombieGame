namespace ZombieGame.Application.Bots.Discussion;

using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public static class DiscussionEventPublisher
{
    public static StructuredDiscussionMessage Publish(
        GameSessionState state,
        StructuredDiscussionMessage message,
        string speakerName,
        string? targetName = null)
    {
        state.DiscussionHistory.Add(message);
        BotDiscussionProcessor.OnStructuredMessage(state, message);
        message.SentAt = DateTime.UtcNow;
        return message;
    }

    public static ChatMessage ToChatMessage(
        StructuredDiscussionMessage message,
        string speakerName,
        string? targetName = null) =>
        new()
        {
            Id = message.Id,
            UserId = message.SpeakerUserId,
            Username = speakerName,
            MessageType = message.MessageType,
            TargetUserId = message.TargetUserId,
            IsStructured = true,
            Text = DiscussionMessageFormatter.Format(speakerName, message.MessageType, targetName),
            SentAt = message.SentAt
        };

    public static void PublishAnnouncements(GameSessionState state, IEnumerable<GlobalDiscussionAnnouncement> announcements) =>
        BotDiscussionProcessor.OnAnnouncements(state, announcements);
}
