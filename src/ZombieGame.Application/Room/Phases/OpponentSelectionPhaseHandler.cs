namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Application.Common;
using ZombieGame.Application.Room;
using ZombieGame.Application.Room.Battle;
using ZombieGame.Application.Room.Bots;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public sealed class OpponentSelectionPhaseHandler : IRoomPhaseHandler
{
    private readonly OpponentSelectionService _opponentSelection;
    private readonly InvitationCoordinator _invitations;
    private readonly IBattleService _battleService;
    private readonly IMatchEventLogService _eventLog;

    public OpponentSelectionPhaseHandler(
        OpponentSelectionService opponentSelection,
        InvitationCoordinator invitations,
        IBattleService battleService,
        IMatchEventLogService eventLog)
    {
        _opponentSelection = opponentSelection;
        _invitations = invitations;
        _battleService = battleService;
        _eventLog = eventLog;
    }

    public RoomPhase Phase => RoomPhase.OpponentSelection;

    public Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        context.Room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.OpponentSelectionSeconds);
        RoomBotScheduler.ScheduleOpponentSelectionBots(context.Room, context.Settings);
        return Task.FromResult(RoomTransitionResult.Stay("Select your opponent."));
    }

    public async Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        var expired = ExpireTimedOutInvitations(room);

        var required = PhaseReadyService.GetRequiredForPhase(room);
        if (PhaseReadyService.IsAllReady(room, required))
        {
            await FinalizeUnmatchedAsync(context, cancellationToken);
            BattlePreparationPhaseHandler.AssignRestingPlayers(room);
            return RoomTransitionResult.Go(RoomPhase.BattlePreparation, "All players ready.", new PhaseChangedEvent(RoomPhase.BattlePreparation, "Preparing Battles..."));
        }

        if (room.PhaseEndsAt is null || DateTime.UtcNow < room.PhaseEndsAt)
        {
            return expired.Count > 0
                ? RoomTransitionResult.Stay("Invitations expired.", new InvitationsCancelledEvent(expired))
                : RoomTransitionResult.Stay();
        }

        await FinalizeUnmatchedAsync(context, cancellationToken);
        BattlePreparationPhaseHandler.AssignRestingPlayers(room);
        return RoomTransitionResult.Go(RoomPhase.BattlePreparation, "Opponent selection closed.", new PhaseChangedEvent(RoomPhase.BattlePreparation, "Preparing Battles..."));
    }

    /// <summary>
    /// Frees players whose invitation was never answered within <c>InvitationTimeoutSeconds</c>
    /// so both sides can invite again before the phase closes.
    /// </summary>
    private static List<BattleInvitation> ExpireTimedOutInvitations(RoomState room)
    {
        var now = DateTime.UtcNow;
        var expired = new List<BattleInvitation>();

        foreach (var invitation in room.PendingInvitations)
        {
            if (invitation.Status != BattleInvitationStatus.Pending)
                continue;

            if (invitation.ExpiresAt is null || invitation.ExpiresAt > now)
                continue;

            invitation.Status = BattleInvitationStatus.Expired;
            expired.Add(invitation);

            var sender = room.GetPlayer(invitation.FromUserId);
            if (sender is not null && !room.IsPaired(sender.UserId))
                sender.HasSentInvitationToday = false;
        }

        return expired;
    }

    public async Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default)
    {
        if (command is MarkPhaseReadyCommand ready)
            return HandleReady(context.Room, ready.UserId);

        return command switch
        {
            SendInvitationCommand send => await HandleSendInvitationAsync(context, send, cancellationToken),
            RespondInvitationCommand respond => await HandleRespondInvitationAsync(context, respond, cancellationToken),
            SendChatCommand chat => await HandleChatAsync(context, chat, cancellationToken),
            _ => RoomTransitionResult.Stay("Invalid command for opponent selection.")
        };
    }

    private RoomTransitionResult HandleReady(RoomState room, Guid userId)
    {
        var required = PhaseReadyService.GetRequiredForPhase(room);
        if (PhaseReadyService.MarkReady(room, userId, required) && _opponentSelection.AllAlivePlayersPaired(room))
        {
            BattlePreparationPhaseHandler.AssignRestingPlayers(room);
            return RoomTransitionResult.Go(RoomPhase.BattlePreparation, "All players ready.");
        }

        return RoomTransitionResult.Stay("Ready recorded.");
    }

    private async Task<RoomTransitionResult> HandleSendInvitationAsync(
        RoomContext context,
        SendInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var room = context.Room;
        var sender = room.GetPlayer(command.FromUserId)
            ?? throw new ServiceException("Player not found.");

        if (!sender.IsAlive)
            throw new ServiceException("Dead players cannot invite.");

        if (sender.HasSentInvitationToday)
            throw new ServiceException("You already sent an invitation today.");

        if (room.IsPaired(sender.UserId))
            throw new ServiceException("You are already paired.");

        if (room.HasPendingInvitation(sender.UserId))
            throw new ServiceException("You already have an invitation awaiting an answer.");

        var target = room.GetPlayer(command.TargetUserId)
            ?? throw new ServiceException("Target not found.");

        if (!target.IsAlive)
            throw new ServiceException("Cannot invite a dead player.");

        if (room.IsPaired(target.UserId))
            throw new ServiceException("Target is already paired.");

        if (room.HasPendingInvitation(target.UserId))
            throw new ServiceException("This player is reviewing another invitation.");

        var available = await _opponentSelection.GetAvailableOpponentsAsync(
            room, sender.UserId, context.History, cancellationToken);

        if (!available.Contains(command.TargetUserId))
            throw new ServiceException("This opponent is not available.");

        var sentAt = DateTime.UtcNow;
        var invitation = new BattleInvitation
        {
            Id = Guid.NewGuid(),
            FromUserId = command.FromUserId,
            ToUserId = command.TargetUserId,
            SentAt = sentAt,
            ExpiresAt = sentAt.AddSeconds(context.Settings.InvitationTimeoutSeconds)
        };

        room.PendingInvitations.Add(invitation);
        sender.HasSentInvitationToday = true;
        RoomActivityTracker.MarkActed(room, sender.UserId);

        if (target.IsBot)
            RoomBotScheduler.ScheduleInvitationAccept(invitation, context.Settings);

        await _eventLog.LogAsync(room.MatchId, MatchEventType.InvitationSent, command.FromUserId, new { invitation.Id, command.TargetUserId }, cancellationToken);

        if (_opponentSelection.AllAlivePlayersPaired(room))
        {
            BattlePreparationPhaseHandler.AssignRestingPlayers(room);
            return RoomTransitionResult.Go(RoomPhase.BattlePreparation, "All players paired.", new InvitationSentEvent(invitation));
        }

        return RoomTransitionResult.Stay("Invitation sent.", new InvitationSentEvent(invitation));
    }

    private async Task<RoomTransitionResult> HandleRespondInvitationAsync(
        RoomContext context,
        RespondInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var room = context.Room;
        var invitation = room.PendingInvitations.FirstOrDefault(i => i.Id == command.InvitationId)
            ?? throw new ServiceException("Invitation not found.");

        if (invitation.ToUserId != command.UserId)
            throw new ServiceException("Not your invitation.");

        if (invitation.Status != BattleInvitationStatus.Pending)
            throw new ServiceException("Invitation already handled.");

        RoomActivityTracker.MarkActed(room, command.UserId);

        if (!command.Accept)
        {
            invitation.Status = BattleInvitationStatus.Rejected;
            // Reject unlocks both sides: sender may invite someone else again today.
            var sender = room.GetPlayer(invitation.FromUserId);
            if (sender is not null && !room.IsPaired(sender.UserId))
                sender.HasSentInvitationToday = false;
            return RoomTransitionResult.Stay("Invitation rejected.");
        }

        var acceptResult = _invitations.OnInvitationAccepted(room, invitation);
        var battle = await _battleService.CreateBattleAsync(
            room,
            acceptResult.Accepted.FromUserId,
            acceptResult.Accepted.ToUserId,
            TimeSpan.FromSeconds(context.Settings.CardBattleSeconds),
            cancellationToken);

        var pair = _battleService.CreatePairReference(battle);
        room.BattlePairs.Add(pair);

        await context.History.RecordPairAsync(room.MatchId, pair.Player1Id, pair.Player2Id, cancellationToken);
        await _eventLog.LogAsync(room.MatchId, MatchEventType.InvitationAccepted, command.UserId, new { invitation.Id, battle.BattleId }, cancellationToken);

        var events = new List<RoomEvent> { new InvitationAcceptedEvent(invitation, pair) };
        if (acceptResult.CancelledInvitations.Count > 0)
            events.Add(new InvitationsCancelledEvent(acceptResult.CancelledInvitations));

        if (_opponentSelection.AllAlivePlayersPaired(room))
        {
            BattlePreparationPhaseHandler.AssignRestingPlayers(room);
            return RoomTransitionResult.Go(RoomPhase.BattlePreparation, "All players paired.", events.ToArray());
        }

        return RoomTransitionResult.Stay("Invitation accepted.", events.ToArray());
    }

    private async Task FinalizeUnmatchedAsync(RoomContext context, CancellationToken cancellationToken)
    {
        var room = context.Room;

        // Exactly two unmatched (bots or humans): force a battle instead of both resting.
        var autoPair = _opponentSelection.TryPairLastTwoUnmatched(room);
        if (autoPair is not null)
        {
            await context.History.RecordPairAsync(
                room.MatchId,
                autoPair.Player1Id,
                autoPair.Player2Id,
                cancellationToken);
            return;
        }

        var unmatched = _opponentSelection.GetUnmatchedPlayers(room);
        if (unmatched.Count != 1)
            return;

        var loneId = unmatched[0];
        switch (context.Settings.UnmatchedPlayerRule)
        {
            case UnmatchedPlayerRule.RandomAssignment:
                var partner = room.AlivePlayers
                    .Where(p => p.UserId != loneId && room.IsPaired(p.UserId))
                    .Select(p => p.UserId)
                    .FirstOrDefault();
                if (partner != Guid.Empty)
                {
                    var existingPair = room.BattlePairs.First(p => p.Player1Id == partner || p.Player2Id == partner);
                    room.BattlePairs.Remove(existingPair);
                    room.GetPlayer(existingPair.Player1Id)!.HasSentInvitationToday = false;
                    room.GetPlayer(existingPair.Player2Id)!.HasSentInvitationToday = false;
                }

                var candidate = room.AlivePlayers
                    .FirstOrDefault(p => p.UserId != loneId && !room.IsPaired(p.UserId));

                if (candidate is not null)
                {
                    var pair = _opponentSelection.CreatePair(loneId, candidate.UserId);
                    room.BattlePairs.Add(pair);
                    await context.History.RecordPairAsync(room.MatchId, pair.Player1Id, pair.Player2Id, cancellationToken);
                }
                break;

            case UnmatchedPlayerRule.Bot:
                // Bot pairing handled by match fill; lone human rests this day.
                break;

            case UnmatchedPlayerRule.Skip:
            default:
                // Single leftover rests (AssignRestingPlayers after finalize).
                break;
        }
    }

    /// <summary>Room chat is allowed during opponent selection (Room Flow V2).</summary>
    private static async Task<RoomTransitionResult> HandleChatAsync(
        RoomContext context,
        SendChatCommand chat,
        CancellationToken cancellationToken)
    {
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
}
