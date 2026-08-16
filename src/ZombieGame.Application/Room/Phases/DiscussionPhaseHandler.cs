namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Application.Bots.Discussion;
using ZombieGame.Application.Common;
using ZombieGame.Application.Room.Bots;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public sealed class DiscussionPhaseHandler : IRoomPhaseHandler
{
    public RoomPhase Phase => RoomPhase.Discussion;

    public async Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        context.Room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.DiscussionPhaseSeconds);
        await context.Chat.ClearAsync(context.Room.MatchId, cancellationToken);
        foreach (var key in context.Room.Session.Metadata.Keys.Where(k => k.StartsWith("bot-discussion-spoke:", StringComparison.Ordinal)).ToList())
            context.Room.Session.Metadata.Remove(key);

        var announcements = DiscussionAnnouncementService.Build(context.Room.Session);
        DiscussionEventPublisher.PublishAnnouncements(context.Room.Session, announcements);

        RoomBotScheduler.ScheduleDiscussionBots(context.Room, context.Settings);
        return RoomTransitionResult.Stay("Discussion started.", new PhaseChangedEvent(RoomPhase.Discussion, "Discussion started."));
    }

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        var required = PhaseReadyService.GetRequiredForPhase(room);
        if (PhaseReadyService.IsAllReady(room, required))
            return Task.FromResult(RoomTransitionResult.Go(RoomPhase.Voting, "All players ready.", new PhaseChangedEvent(RoomPhase.Voting, "Voting started.")));

        if (room.PhaseEndsAt is null || DateTime.UtcNow < room.PhaseEndsAt)
            return Task.FromResult(RoomTransitionResult.Stay());

        return Task.FromResult(RoomTransitionResult.Go(
            RoomPhase.Voting,
            "Voting started.",
            new PhaseChangedEvent(RoomPhase.Voting, "Voting started.")));
    }

    public async Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default)
    {
        if (command is MarkPhaseReadyCommand ready)
        {
            var required = PhaseReadyService.GetRequiredForPhase(context.Room);
            if (PhaseReadyService.MarkReady(context.Room, ready.UserId, required))
                return RoomTransitionResult.Go(RoomPhase.Voting, "All players ready.");
            return RoomTransitionResult.Stay("Ready recorded.");
        }

        if (command is SendStructuredChatCommand structured)
            return await HandleStructuredChatAsync(context, structured, cancellationToken);

        if (command is not SendChatCommand chat)
            return RoomTransitionResult.Stay("Invalid command for discussion.");

        var room = context.Room;
        var player = room.GetPlayer(chat.UserId)
            ?? throw new ServiceException("Player not found.");

        if (!player.IsAlive)
            throw new ServiceException("Dead players cannot chat.");

        if (string.IsNullOrWhiteSpace(chat.Text))
            throw new ServiceException("Message cannot be empty.");

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = chat.UserId,
            Username = player.Username,
            Text = chat.Text.Trim(),
            SentAt = DateTime.UtcNow,
            IsStructured = false
        };

        await context.Chat.AddMessageAsync(room.MatchId, message, cancellationToken);
        RoomActivityTracker.MarkActed(room, chat.UserId);
        return RoomTransitionResult.Stay("Message sent.", new ChatMessageEvent(message));
    }

    private static async Task<RoomTransitionResult> HandleStructuredChatAsync(
        RoomContext context,
        SendStructuredChatCommand command,
        CancellationToken cancellationToken)
    {
        var room = context.Room;
        var player = room.GetPlayer(command.UserId)
            ?? throw new ServiceException("Player not found.");

        if (!player.IsAlive)
            throw new ServiceException("Dead players cannot chat.");

        var targetName = command.TargetUserId is Guid targetId
            ? room.GetPlayer(targetId)?.Username
            : null;

        var structured = new StructuredDiscussionMessage
        {
            SpeakerUserId = command.UserId,
            MessageType = command.MessageType,
            TargetUserId = command.TargetUserId,
            DayNumber = room.DayNumber
        };

        DiscussionEventPublisher.Publish(room.Session, structured, player.Username, targetName);

        var message = DiscussionEventPublisher.ToChatMessage(structured, player.Username, targetName);
        await context.Chat.AddMessageAsync(room.MatchId, message, cancellationToken);
        return RoomTransitionResult.Stay("Message sent.", new ChatMessageEvent(message));
    }
}
