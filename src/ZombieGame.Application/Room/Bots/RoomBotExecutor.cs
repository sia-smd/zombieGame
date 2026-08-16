namespace ZombieGame.Application.Room.Bots;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public interface IRoomBotExecutor
{
    Task ProcessRoomBotsAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public sealed class RoomBotExecutor : IRoomBotExecutor
{
    private readonly IRoomStateMachine _stateMachine;
    private readonly OpponentSelectionService _opponentSelection;
    private readonly IBattlePairHistoryStore _history;
    private readonly IBotService _botService;
    private readonly RoomSettings _settings;
    private readonly ILogger<RoomBotExecutor> _logger;

    public RoomBotExecutor(
        IRoomStateMachine stateMachine,
        OpponentSelectionService opponentSelection,
        IBattlePairHistoryStore history,
        IBotService botService,
        IOptions<RoomSettings> settings,
        ILogger<RoomBotExecutor> logger)
    {
        _stateMachine = stateMachine;
        _opponentSelection = opponentSelection;
        _history = history;
        _botService = botService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ProcessRoomBotsAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (!_settings.RoomBotsEnabled)
            return;

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken);
        if (room is null || room.IsFinished)
            return;

        switch (room.CurrentPhase)
        {
            case RoomPhase.OpponentSelection:
                if (await TryAcceptBotInvitationAsync(matchId, room, cancellationToken))
                    return;
                if (await TrySendBotInvitationAsync(matchId, room, cancellationToken))
                    return;
                await TryBotMarkReadyAsync(matchId, room, cancellationToken);
                break;

            case RoomPhase.CardBattle:
                if (await TryBotBattleActionAsync(matchId, room, cancellationToken))
                    return;
                await TryBotMarkReadyAsync(matchId, room, cancellationToken);
                break;

            case RoomPhase.Discussion:
                if (await TrySendBotChatAsync(matchId, room, cancellationToken))
                    return;
                await TryBotMarkReadyAsync(matchId, room, cancellationToken);
                break;

            case RoomPhase.Voting:
                if (await TryCastBotVoteAsync(matchId, room, cancellationToken))
                    return;
                await TryBotMarkReadyAsync(matchId, room, cancellationToken);
                break;
        }
    }

    private async Task<bool> TryAcceptBotInvitationAsync(
        Guid matchId,
        RoomState room,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var invitation = room.PendingInvitations
            .Where(i => i.Status == BattleInvitationStatus.Pending)
            .Where(i => i.BotRespondAt is not null && i.BotRespondAt <= now)
            .Where(i => room.GetPlayer(i.ToUserId)?.IsBot == true)
            .OrderBy(i => i.BotRespondAt)
            .FirstOrDefault();

        if (invitation is null)
            return false;

        try
        {
            await _stateMachine.DispatchAsync(
                matchId,
                new RespondInvitationCommand(invitation.ToUserId, invitation.Id, Accept: true),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot failed to accept invitation {InvitationId} in match {MatchId}", invitation.Id, matchId);
        }

        return true;
    }

    private async Task<bool> TrySendBotInvitationAsync(
        Guid matchId,
        RoomState room,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var bot = room.AlivePlayers
            .Where(p => p.IsBot)
            .Where(p => !room.IsPaired(p.UserId))
            .Where(p => !p.HasSentInvitationToday)
            .Where(p => p.BotNextActionAt is not null && p.BotNextActionAt <= now)
            .Where(p => !room.PendingInvitations.Any(i =>
                i.Status == BattleInvitationStatus.Pending &&
                (i.FromUserId == p.UserId || i.ToUserId == p.UserId)))
            .OrderBy(p => p.BotNextActionAt)
            .FirstOrDefault();

        if (bot is null)
            return false;

        var available = await _opponentSelection.GetAvailableOpponentsAsync(room, bot.UserId, _history, cancellationToken);
        if (available.Count == 0)
        {
            bot.BotNextActionAt = null;
            return false;
        }

        var targetId = available.ElementAt(Random.Shared.Next(available.Count));
        try
        {
            await _stateMachine.DispatchAsync(
                matchId,
                new SendInvitationCommand(bot.UserId, targetId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot {BotUserId} failed to send invitation in match {MatchId}", bot.UserId, matchId);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(_settings.BotInviteMinDelaySeconds);
        }

        return true;
    }

    private async Task<bool> TryBotBattleActionAsync(
        Guid matchId,
        RoomState room,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var pair in room.BattlePairs.Where(p => p.Status == BattlePairStatus.InProgress))
        {
            foreach (var botId in new[] { pair.Player1Id, pair.Player2Id })
            {
                var bot = room.GetPlayer(botId);
                if (bot is null || !bot.IsBot || RoomBotScheduler.IsBotFinishedInPair(pair, botId))
                    continue;

                if (bot.BotNextActionAt is null || bot.BotNextActionAt > now)
                    continue;

                await ExecuteBotBattleActionAsync(matchId, room, pair, bot, cancellationToken);
                return true;
            }
        }

        return false;
    }

    private async Task ExecuteBotBattleActionAsync(
        Guid matchId,
        RoomState room,
        BattlePair pair,
        RoomPlayerState bot,
        CancellationToken cancellationToken)
    {
        bot.BotNextActionAt = null;

        try
        {
            var opponentId = pair.Player1Id == bot.UserId ? pair.Player2Id : pair.Player1Id;
            var cardPlay = _botService.TryDecideCardPlay(room.Session, bot.UserId, opponentId);
            if (cardPlay is not null)
            {
                await _stateMachine.DispatchAsync(
                    matchId,
                    new BattlePlayCardCommand(bot.UserId, pair.PairId, cardPlay.CardId, cardPlay.TargetUserId),
                    cancellationToken);
                return;
            }

            await _stateMachine.DispatchAsync(
                matchId,
                new BattlePassCommand(bot.UserId, pair.PairId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot {BotUserId} failed battle action in match {MatchId}", bot.UserId, matchId);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(_settings.BotBattleMinDelaySeconds);
        }
    }

    private async Task<bool> TrySendBotChatAsync(
        Guid matchId,
        RoomState room,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var bot = room.AlivePlayers
            .Where(p => p.IsBot)
            .Where(p => !HasBotSpoke(room, p.UserId))
            .Where(p => p.BotNextActionAt is not null && p.BotNextActionAt <= now)
            .OrderBy(p => p.BotNextActionAt)
            .FirstOrDefault();

        if (bot is null)
            return false;

        if (!_botService.ShouldSpeakInDiscussion(room.Session, bot.UserId))
        {
            MarkBotSpoke(room, bot.UserId);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(_settings.BotReadyMinDelaySeconds);
            return true;
        }

        var structured = _botService.TryDecideDiscussionMessage(room.Session, bot.UserId);
        if (structured is null)
        {
            MarkBotSpoke(room, bot.UserId);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(_settings.BotReadyMinDelaySeconds);
            return true;
        }

        bot.BotNextActionAt = null;

        try
        {
            await _stateMachine.DispatchAsync(
                matchId,
                new SendStructuredChatCommand(bot.UserId, structured.MessageType, structured.TargetUserId),
                cancellationToken);
            _botService.RecordDiscussionSpoke(room.Session, bot.UserId);
            MarkBotSpoke(room, bot.UserId);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(
                Random.Shared.Next(_settings.BotReadyMinDelaySeconds, _settings.BotReadyMaxDelaySeconds + 1));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot {BotUserId} failed to chat in match {MatchId}", bot.UserId, matchId);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(_settings.BotChatMinDelaySeconds);
        }

        return true;
    }

    private async Task<bool> TryCastBotVoteAsync(
        Guid matchId,
        RoomState room,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var bot = room.AlivePlayers
            .Where(p => p.IsBot)
            .Where(p => !room.Votes.ContainsKey(p.UserId))
            .Where(p => p.BotNextActionAt is not null && p.BotNextActionAt <= now)
            .OrderBy(p => p.BotNextActionAt)
            .FirstOrDefault();

        if (bot is null)
            return false;

        bot.BotNextActionAt = null;

        try
        {
            var targetId = _botService.TryDecideVoteTarget(room.Session, bot.UserId);
            await _stateMachine.DispatchAsync(
                matchId,
                new CastVoteCommand(bot.UserId, targetId),
                cancellationToken);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(
                Random.Shared.Next(_settings.BotReadyMinDelaySeconds, _settings.BotReadyMaxDelaySeconds + 1));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot {BotUserId} failed to vote in match {MatchId}", bot.UserId, matchId);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(_settings.BotVoteMinDelaySeconds);
        }

        return true;
    }

    private async Task TryBotMarkReadyAsync(
        Guid matchId,
        RoomState room,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var required = PhaseReadyService.GetRequiredForPhase(room).ToHashSet();
        var bot = room.AlivePlayers
            .Where(p => p.IsBot)
            .Where(p => required.Count == 0 || required.Contains(p.UserId))
            .Where(p => !room.PhaseReadyPlayers.Contains(p.UserId))
            .Where(p => IsBotReadyForPhase(room, p))
            .Where(p => p.BotNextActionAt is not null && p.BotNextActionAt <= now)
            .OrderBy(p => p.BotNextActionAt)
            .FirstOrDefault();

        if (bot is null)
            return;

        bot.BotNextActionAt = null;

        try
        {
            await _stateMachine.DispatchAsync(
                matchId,
                new MarkPhaseReadyCommand(bot.UserId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot {BotUserId} failed to mark ready in match {MatchId}", bot.UserId, matchId);
            bot.BotNextActionAt = DateTime.UtcNow.AddSeconds(_settings.BotReadyMinDelaySeconds);
        }
    }

    private static bool IsBotReadyForPhase(RoomState room, RoomPlayerState bot) =>
        room.CurrentPhase switch
        {
            RoomPhase.Discussion => HasBotSpoke(room, bot.UserId),
            RoomPhase.Voting => room.Votes.ContainsKey(bot.UserId),
            RoomPhase.CardBattle => room.BattlePairs
                .Where(p => p.Status == BattlePairStatus.InProgress)
                .Any(p => (p.Player1Id == bot.UserId || p.Player2Id == bot.UserId) &&
                          RoomBotScheduler.IsBotFinishedInPair(p, bot.UserId)),
            _ => true
        };

    private static bool HasBotSpoke(RoomState room, Guid botUserId) =>
        room.Session.Metadata.TryGetValue(BotSpokeKey(botUserId), out var value) &&
        value is true;

    private static void MarkBotSpoke(RoomState room, Guid botUserId) =>
        room.Session.Metadata[BotSpokeKey(botUserId)] = true;

    private static string BotSpokeKey(Guid botUserId) => $"bot-discussion-spoke:{botUserId}";
}
