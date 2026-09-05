namespace ZombieGame.Application.Services;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Room;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public sealed class RoomService : IRoomService
{
    private readonly IRoomStateMachine _stateMachine;
    private readonly IMatchRepository _matchRepository;
    private readonly IBattlePairHistoryStore _history;
    private readonly IDiscussionChatStore _chat;
    private readonly OpponentSelectionService _opponentSelection;
    private readonly IRoomCommandValidator _validator;
    private readonly IPlayerReconnectService _reconnect;
    private readonly IRoomLifecycleCoordinator _lifecycle;
    private readonly IRoomStatePresenter _presenter;
    private readonly GameSettings _gameSettings;
    private readonly RoomSettings _roomSettings;
    private readonly IUnitOfWork _unitOfWork;

    public RoomService(
        IRoomStateMachine stateMachine,
        IMatchRepository matchRepository,
        IBattlePairHistoryStore history,
        IDiscussionChatStore chat,
        OpponentSelectionService opponentSelection,
        IRoomCommandValidator validator,
        IPlayerReconnectService reconnect,
        IRoomLifecycleCoordinator lifecycle,
        IRoomStatePresenter presenter,
        IOptions<GameSettings> gameSettings,
        IOptions<RoomSettings> roomSettings,
        IUnitOfWork unitOfWork)
    {
        _stateMachine = stateMachine;
        _matchRepository = matchRepository;
        _history = history;
        _chat = chat;
        _opponentSelection = opponentSelection;
        _validator = validator;
        _reconnect = reconnect;
        _lifecycle = lifecycle;
        _presenter = presenter;
        _gameSettings = gameSettings.Value;
        _roomSettings = roomSettings.Value;
        _unitOfWork = unitOfWork;
    }

    public async Task<RoomActionResult> JoinRoomAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var match = await ValidateAccessAsync(userId, matchId, sessionToken, cancellationToken);
        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken);

        if (room is null)
            room = await _stateMachine.InitializeRoomAsync(match, cancellationToken);
        else
        {
            await _stateMachine.SyncPlayersFromMatchAsync(matchId, cancellationToken);
            room = await _stateMachine.GetStateAsync(matchId, cancellationToken) ?? room;
        }

        await _lifecycle.SyncPlayerPresenceAsync(room, userId, sessionToken, cancellationToken);
        room = await _stateMachine.SetPresenceAsync(matchId, userId, connected: true, cancellationToken) ?? room;
        return await OkAsync(userId, room, "Joined room.", cancellationToken);
    }

    public async Task<RoomActionResult> StartGameAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var match = await ValidateAccessAsync(userId, matchId, sessionToken, cancellationToken);

        if (match.Status != MatchStatus.Waiting)
        {
            var existing = await _stateMachine.GetStateAsync(matchId, cancellationToken);
            return existing is null
                ? new RoomActionResult(false, "Match has already started.")
                : await OkAsync(userId, existing, "Game already started.", cancellationToken);
        }

        var fresh = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken) ?? match;
        if (fresh.Players.Count < fresh.MaxPlayers)
            throw new ServiceException($"Room is not full yet ({fresh.Players.Count}/{fresh.MaxPlayers}).");

        if (fresh.Players.Count < 2)
            throw new ServiceException("Need at least 2 players to start.");

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)
            ?? await _stateMachine.InitializeRoomAsync(match, cancellationToken);

        await _stateMachine.SyncPlayersFromMatchAsync(matchId, cancellationToken);
        room = await _stateMachine.GetStateAsync(matchId, cancellationToken) ?? room;

        var result = await _stateMachine.StartGameAsync(matchId, cancellationToken);

        match.Status = MatchStatus.InProgress;
        match.StartedAt = DateTime.UtcNow;
        _matchRepository.Update(match);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-fetch after SaveChanges: DayStart is only a few seconds long, so the room loop
        // may already have advanced (e.g. to OpponentSelection). A Redis snapshot taken before
        // SaveChanges would otherwise re-broadcast stale DayStart and pin the client dialog open.
        room = await _stateMachine.GetStateAsync(matchId, cancellationToken) ?? room;
        return await OkAsync(userId, room, result.Message, cancellationToken);
    }

    public async Task<RoomActionResult> SendInvitationAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidatePlayerCommandAsync(userId, matchId, sessionToken, RoomPhase.OpponentSelection, cancellationToken: cancellationToken);
        var result = await _stateMachine.DispatchAsync(
            matchId,
            new SendInvitationCommand(userId, targetUserId),
            cancellationToken);

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)!;
        return await OkAsync(userId, room!, result.Message, cancellationToken);
    }

    public async Task<RoomActionResult> RespondInvitationAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid invitationId,
        bool accept,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidatePlayerCommandAsync(userId, matchId, sessionToken, RoomPhase.OpponentSelection, cancellationToken: cancellationToken);
        var result = await _stateMachine.DispatchAsync(
            matchId,
            new RespondInvitationCommand(userId, invitationId, accept),
            cancellationToken);

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)!;
        return await OkAsync(userId, room!, result.Message, cancellationToken);
    }

    public async Task<RoomActionResult> PlayCardInBattleAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid pairId,
        Guid cardId,
        Guid? targetUserId,
        int? inventorySlotIndex = null,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateBattleCommandAsync(userId, matchId, sessionToken, pairId, cancellationToken);
        var result = await _stateMachine.DispatchAsync(
            matchId,
            new BattlePlayCardCommand(userId, pairId, cardId, targetUserId, inventorySlotIndex),
            cancellationToken);

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)!;
        return await OkAsync(userId, room!, result.Message, cancellationToken);
    }

    public async Task<RoomActionResult> FinishBattleTurnAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid pairId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateBattleCommandAsync(userId, matchId, sessionToken, pairId, cancellationToken);
        var result = await _stateMachine.DispatchAsync(
            matchId,
            new BattleFinishTurnCommand(userId, pairId),
            cancellationToken);

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)!;
        return await OkAsync(userId, room!, result.Message, cancellationToken);
    }

    public async Task<RoomActionResult> PassInBattleAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid pairId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateBattleCommandAsync(userId, matchId, sessionToken, pairId, cancellationToken);
        var result = await _stateMachine.DispatchAsync(
            matchId,
            new BattleFinishTurnCommand(userId, pairId),
            cancellationToken);

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)!;
        return await OkAsync(userId, room!, result.Message, cancellationToken);
    }

    public async Task<RoomActionResult> SendChatAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        string text,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidatePlayerCommandAsync(
            userId,
            matchId,
            sessionToken,
            new[] { RoomPhase.Discussion, RoomPhase.OpponentSelection },
            cancellationToken: cancellationToken);
        var result = await _stateMachine.DispatchAsync(
            matchId,
            new SendChatCommand(userId, text),
            cancellationToken);

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)!;
        return await OkAsync(userId, room!, result.Message, cancellationToken);
    }

    public async Task<RoomActionResult> VoteAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidatePlayerCommandAsync(userId, matchId, sessionToken, RoomPhase.Voting, cancellationToken: cancellationToken);
        var result = await _stateMachine.DispatchAsync(
            matchId,
            new CastVoteCommand(userId, targetUserId),
            cancellationToken);

        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)!;
        return await OkAsync(userId, room!, result.Message, cancellationToken);
    }

    public async Task<RoomStateDto?> GetPublicStateAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        await ValidateAccessAsync(userId, matchId, sessionToken, cancellationToken);
        await _stateMachine.SyncPlayersFromMatchAsync(matchId, cancellationToken);
        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken);
        if (room is null)
            return null;

        // Caller-scoped: carries the viewer's own hand/health for the battle screen.
        return await _presenter.BuildPrivateAsync(room, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetDiscussionAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var messages = await _chat.GetMessagesAsync(matchId, cancellationToken);
        return messages.Select(m => new ChatMessageDto(m.Id, m.UserId, m.Username, m.Text, m.SentAt)).ToList();
    }

    public async Task<RoomActionResult> MarkPhaseReadyAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var room = await _stateMachine.GetStateAsync(matchId, cancellationToken)
            ?? throw new ServiceException("Room not found.");

        await _validator.ValidatePlayerCommandAsync(userId, matchId, sessionToken, room.CurrentPhase, cancellationToken: cancellationToken);

        var result = await _stateMachine.DispatchAsync(matchId, new MarkPhaseReadyCommand(userId), cancellationToken);
        room = await _stateMachine.GetStateAsync(matchId, cancellationToken)!;
        return await OkAsync(userId, room!, result.Message, cancellationToken);
    }

    public async Task<JoinBattleResult> JoinBattleAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        Guid battleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _validator.ValidateBattleCommandAsync(userId, matchId, sessionToken, battleId, cancellationToken);
            return new JoinBattleResult(true, battleId);
        }
        catch (ServiceException ex)
        {
            return new JoinBattleResult(false, battleId, ex.Message);
        }
    }

    public async Task<ResumeMatchResponse?> ResumeMatchAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        await ValidateAccessAsync(userId, matchId, sessionToken, cancellationToken);
        var resume = await _reconnect.ResumeAsync(userId, matchId, sessionToken, cancellationToken);
        if (resume is not null)
            await _stateMachine.SetPresenceAsync(matchId, userId, connected: true, cancellationToken);

        return resume;
    }

    public async Task<RoomActionResult?> OnPlayerDisconnectedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entry = await _reconnect.OnDisconnectedAsync(userId, cancellationToken);
        if (entry is null)
            return null;

        var room = await _stateMachine.SetPresenceAsync(entry.MatchId, userId, connected: false, cancellationToken);
        if (room is null)
            return null;

        return await OkAsync(userId, room, "Player disconnected.", cancellationToken);
    }

    private async Task<RoomActionResult> OkAsync(
        Guid userId,
        Domain.Models.Room.RoomState room,
        string message,
        CancellationToken cancellationToken) =>
        new(true, message, await _presenter.BuildAsync(room, userId, cancellationToken));

    private async Task<Domain.Entities.Match> ValidateAccessAsync(
        Guid userId,
        Guid matchId,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken)
            ?? throw new ServiceException("Match not found.");

        if (!string.Equals(match.SessionToken, sessionToken, StringComparison.Ordinal))
            throw new ServiceException("Invalid session token.");

        if (!match.Players.Any(p => p.UserId == userId))
            throw new ServiceException("User is not in this match.");

        return match;
    }
}
