namespace ZombieGame.Application.Room;

using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public sealed class RoomContext
{
    public required RoomState Room { get; init; }
    public required RoomSettings Settings { get; init; }
    public required IBattlePairHistoryStore History { get; init; }
    public required IDiscussionChatStore Chat { get; init; }
    public Guid? ActingUserId { get; init; }
}

public sealed class RoomTransitionResult
{
    public bool Advanced { get; init; }
    public RoomPhase? NextPhase { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<RoomEvent> Events { get; init; } = Array.Empty<RoomEvent>();

    public static RoomTransitionResult Stay(string message = "") =>
        new() { Message = message };

    public static RoomTransitionResult Stay(string message, params RoomEvent[] events) =>
        new() { Message = message, Events = events };

    public static RoomTransitionResult Go(RoomPhase next, string message, params RoomEvent[] events) =>
        new() { Advanced = true, NextPhase = next, Message = message, Events = events };
}

public abstract record RoomEvent;

public sealed record PhaseChangedEvent(RoomPhase Phase, string Message) : RoomEvent;
public sealed record InvitationSentEvent(BattleInvitation Invitation) : RoomEvent;
public sealed record InvitationAcceptedEvent(BattleInvitation Invitation, BattlePair Pair) : RoomEvent;
public sealed record BattleStartedEvent(BattlePair Pair) : RoomEvent;
public sealed record BattleFinishedEvent(BattleSummary Summary) : RoomEvent;
public sealed record PlayerEliminatedEvent(Guid PlayerId) : RoomEvent;
public sealed record DayStartedEvent(int DayNumber, DayEventType DayEvent) : RoomEvent;
public sealed record GameFinishedEvent(WinTeam Winner) : RoomEvent;
public sealed record ChatMessageEvent(ChatMessage Message) : RoomEvent;
public sealed record VoteUpdatedEvent(int VoteCount) : RoomEvent;

public interface IRoomPhaseHandler
{
    RoomPhase Phase { get; }
    Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default);
    Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default);
    Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default);
}

public interface IRoomCommand { }

public sealed record SendInvitationCommand(Guid FromUserId, Guid TargetUserId) : IRoomCommand;
public sealed record RespondInvitationCommand(Guid UserId, Guid InvitationId, bool Accept) : IRoomCommand;
public sealed record BattlePlayCardCommand(Guid UserId, Guid PairId, Guid CardId, Guid? TargetUserId) : IRoomCommand;
public sealed record BattlePassCommand(Guid UserId, Guid PairId) : IRoomCommand;
public sealed record SendChatCommand(Guid UserId, string Text) : IRoomCommand;
public sealed record SendStructuredChatCommand(
    Guid UserId,
    DiscussionMessageType MessageType,
    Guid? TargetUserId) : IRoomCommand;
public sealed record CastVoteCommand(Guid UserId, Guid TargetUserId) : IRoomCommand;
public sealed record MarkPhaseReadyCommand(Guid UserId) : IRoomCommand;
