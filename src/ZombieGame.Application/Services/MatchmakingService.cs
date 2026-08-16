namespace ZombieGame.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Matchmaking;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public class MatchmakingService : IMatchmakingService
{
    private readonly IMatchmakingQueue _queue;
    private readonly IMatchmakingPendingMatchStore _pendingMatches;
    private readonly IMatchRepository _matchRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICoinService _coinService;
    private readonly IBotService _botService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoomStateMachine _roomStateMachine;
    private readonly IPlayerActiveMatchStore _playerActiveMatches;
    private readonly IUserPresenceTracker _presence;
    private readonly IRoomInviteStore _invites;
    private readonly IRoomRealtimeNotifier _realtime;
    private readonly GameSettings _settings;
    private readonly ILogger<MatchmakingService> _logger;
    private readonly SemaphoreSlim _matchCreationGate = new(1, 1);

    public MatchmakingService(
        IMatchmakingQueue queue,
        IMatchmakingPendingMatchStore pendingMatches,
        IMatchRepository matchRepository,
        IUserRepository userRepository,
        ICoinService coinService,
        IBotService botService,
        IUnitOfWork unitOfWork,
        IRoomStateMachine roomStateMachine,
        IPlayerActiveMatchStore playerActiveMatches,
        IOptions<GameSettings> settings,
        ILogger<MatchmakingService> logger,
        IUserPresenceTracker? presence = null,
        IRoomInviteStore? invites = null,
        IRoomRealtimeNotifier? realtime = null)
    {
        _queue = queue;
        _pendingMatches = pendingMatches;
        _matchRepository = matchRepository;
        _userRepository = userRepository;
        _coinService = coinService;
        _botService = botService;
        _unitOfWork = unitOfWork;
        _roomStateMachine = roomStateMachine;
        _playerActiveMatches = playerActiveMatches;
        _presence = presence ?? new UserPresenceTracker();
        _invites = invites ?? new RoomInviteStore();
        _realtime = realtime ?? new NullRoomRealtimeNotifier();
        _settings = settings.Value;
        _logger = logger;
    }

    public int QueueCount => _queue.Count;

    public bool IsInQueue(Guid userId) => _queue.Contains(userId);

    public RoomConfigResponse GetRoomConfig() =>
        new(
            _settings.MinRoomPlayers,
            _settings.MaxRoomPlayers,
            _settings.MatchmakingPlayerCount,
            _settings.MatchEntryFeeCoins,
            _settings.FillWithBotsWhenUnderCapacity,
            _settings.MatchmakingBotFillTimeoutSeconds);

    public async Task<CreateRoomResponse> CreateRoomAsync(
        Guid userId,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        var roomName = string.IsNullOrWhiteSpace(request.RoomName)
            ? "Survivor Squad"
            : request.RoomName.Trim();

        if (roomName.Length > 40)
            throw new ServiceException("Room name cannot exceed 40 characters.");

        var maxPlayers = request.MaxPlayers;
        if (maxPlayers < _settings.MinRoomPlayers || maxPlayers > _settings.MaxRoomPlayers)
            throw new ServiceException(
                $"Max players must be between {_settings.MinRoomPlayers} and {_settings.MaxRoomPlayers}.");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException("User not found.");

        if (!await _coinService.CanAffordEntryFeeAsync(userId, cancellationToken))
            throw new ServiceException($"Insufficient coins. Entry fee is {_settings.MatchEntryFeeCoins} coins.");

        _queue.TryRemove(userId);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.Waiting,
            CurrentPhase = GamePhase.Lobby,
            SessionToken = Guid.NewGuid().ToString("N"),
            Name = roomName,
            MaxPlayers = maxPlayers,
            FillWithBots = request.FillWithBots,
            CreatedAt = DateTime.UtcNow,
            Players =
            {
                new MatchPlayer
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SeatIndex = 0,
                    JoinedAt = DateTime.UtcNow
                }
            }
        };

        await _matchRepository.AddAsync(match, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Bots are filled later by MatchmakingTimeoutHostedService after MatchmakingBotFillTimeoutSeconds.
        _logger.LogInformation(
            "Created waiting room {MatchId} ({RoomName}) for {MaxPlayers} players. FillWithBots={FillWithBots}, bot delay={Delay}s.",
            match.Id,
            roomName,
            match.MaxPlayers,
            match.FillWithBots,
            _settings.MatchmakingBotFillTimeoutSeconds);

        _pendingMatches.Set(userId, match.Id, match.SessionToken);

        return new CreateRoomResponse(
            match.Id,
            match.SessionToken,
            roomName,
            match.MaxPlayers,
            match.FillWithBots,
            _settings.MatchEntryFeeCoins);
    }

    public async Task<IReadOnlyList<OpenRoomDto>> ListOpenRoomsAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await _matchRepository.GetOpenWaitingRoomsAsync(cancellationToken);
        return rooms.Select(ToOpenRoomDto).ToList();
    }

    public async Task<JoinOpenRoomResponse> JoinOpenRoomAsync(
        Guid userId,
        JoinOpenRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        var match = await ResolveOpenRoomAsync(request, cancellationToken)
            ?? throw new ServiceException("Room not found or no longer available.");

        if (match.Status != MatchStatus.Waiting)
            throw new ServiceException("Room is no longer accepting players.");

        if (match.Players.Count >= match.MaxPlayers
            && match.Players.All(p => p.UserId != userId))
            throw new ServiceException("Room is full.");

        var existingSeat = match.Players.FirstOrDefault(p => p.UserId == userId && !p.IsBot);
        if (existingSeat is not null)
        {
            _pendingMatches.Set(userId, match.Id, match.SessionToken);
            return ToJoinResponse(match);
        }

        await EnsureUserNotInAnotherActiveMatchAsync(userId, match.Id, cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException("User not found.");

        if (!await _coinService.CanAffordEntryFeeAsync(userId, cancellationToken))
            throw new ServiceException($"Insufficient coins. Entry fee is {_settings.MatchEntryFeeCoins} coins.");

        _queue.TryRemove(userId);

        var seatIndex = match.Players.Count == 0
            ? 0
            : match.Players.Max(p => p.SeatIndex) + 1;

        var player = new MatchPlayer
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            UserId = userId,
            SeatIndex = seatIndex,
            JoinedAt = DateTime.UtcNow
        };

        await _matchRepository.AddPlayerAsync(player, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _roomStateMachine.SyncPlayersFromMatchAsync(match.Id, cancellationToken);

        _pendingMatches.Set(userId, match.Id, match.SessionToken);

        _logger.LogInformation(
            "User {UserId} joined open room {MatchId} ({RoomName}) as seat {SeatIndex}.",
            userId,
            match.Id,
            match.Name,
            seatIndex);

        return ToJoinResponse(match);
    }

    public async Task LeaveWaitingRoomAsync(
        Guid userId,
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken)
            ?? throw new ServiceException("Room not found.");

        if (match.Status != MatchStatus.Waiting)
            throw new ServiceException("Can only leave a waiting room.");

        var player = match.Players.FirstOrDefault(p => p.UserId == userId && !p.IsBot)
            ?? throw new ServiceException("You are not in this room.");

        _matchRepository.RemovePlayer(player);
        match.Players.Remove(player);

        _pendingMatches.Remove(userId);
        await _playerActiveMatches.RemoveAsync(userId, cancellationToken);

        var humansLeft = match.Players.Any(p => !p.IsBot);
        if (!humansLeft)
        {
            match.Status = MatchStatus.Finished;
            match.FinishedAt = DateTime.UtcNow;
            _matchRepository.Update(match);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var remaining in match.Players)
                await _playerActiveMatches.RemoveAsync(remaining.UserId, cancellationToken);

            await _roomStateMachine.DiscardLobbyRoomAsync(match.Id, cancellationToken);
            _logger.LogInformation("Waiting room {MatchId} deleted after last human left.", match.Id);
            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _roomStateMachine.RemovePlayerFromLobbyAsync(match.Id, userId, cancellationToken);
        await _roomStateMachine.SyncPlayersFromMatchAsync(match.Id, cancellationToken);

        _logger.LogInformation("User {UserId} left waiting room {MatchId}.", userId, match.Id);
    }

    public async Task<SendRoomInviteResponse> SendWaitingRoomInviteAsync(
        Guid fromUserId,
        SendRoomInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = (request.Username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new ServiceException("Enter a username.", "inviteErrors.userNotFound");

        var match = await _matchRepository.GetWithPlayersAsync(request.MatchId, cancellationToken)
            ?? throw new ServiceException("Room not found.", "inviteErrors.roomNotFound");

        if (match.Status != MatchStatus.Waiting)
            throw new ServiceException("Invites are only allowed in a waiting room.", "inviteErrors.notWaiting");

        if (!match.Players.Any(p => p.UserId == fromUserId && !p.IsBot))
            throw new ServiceException("You are not in this room.", "inviteErrors.notMember");

        if (match.Players.Count >= match.MaxPlayers)
            throw new ServiceException("Room is full.", "inviteErrors.roomFull");

        var target = await _userRepository.GetByUsernameAsync(username, cancellationToken)
            ?? throw new ServiceException("User not found.", "inviteErrors.userNotFound");

        if (target.Id == fromUserId)
            throw new ServiceException("You cannot invite yourself.", "inviteErrors.self");

        if (IsBotUser(target, match) || await IsBotInAnyMatchAsync(target.Id, cancellationToken))
            throw new ServiceException("Cannot invite a bot.", "inviteErrors.bot");

        if (match.Players.Any(p => p.UserId == target.Id && !p.IsBot))
            throw new ServiceException("That player is already in this room.", "inviteErrors.alreadyInRoom");

        try
        {
            await EnsureUserNotInAnotherActiveMatchAsync(target.Id, match.Id, cancellationToken);
        }
        catch (ServiceException)
        {
            throw new ServiceException(
                "That player is already in another match.",
                "inviteErrors.alreadyInMatch");
        }

        if (!_presence.IsOnline(target.Id))
            throw new ServiceException("User is offline.", "inviteErrors.userOffline");

        var ttl = Math.Max(10, _settings.RoomInviteTtlSeconds);
        var invite = new RoomInvite
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            FromUserId = fromUserId,
            ToUserId = target.Id,
            ExpiresAt = DateTime.UtcNow.AddSeconds(ttl),
            Status = RoomInviteStatus.Pending
        };
        _invites.Upsert(invite);

        var fromUser = await _userRepository.GetByIdAsync(fromUserId, cancellationToken);
        var payload = new RoomInviteReceivedDto(
            invite.Id,
            match.Id,
            string.IsNullOrWhiteSpace(match.Name) ? "Survivor Squad" : match.Name!,
            ToRoomCode(match.Id),
            fromUser?.Username ?? "Player",
            invite.ExpiresAt);

        await _realtime.RoomInviteReceivedAsync(target.Id, payload, cancellationToken);

        _logger.LogInformation(
            "User {FromUserId} invited {ToUserId} to waiting room {MatchId} (invite {InviteId}).",
            fromUserId,
            target.Id,
            match.Id,
            invite.Id);

        return new SendRoomInviteResponse(invite.Id, invite.ExpiresAt);
    }

    public async Task<JoinOpenRoomResponse> AcceptWaitingRoomInviteAsync(
        Guid userId,
        Guid inviteId,
        CancellationToken cancellationToken = default)
    {
        var invite = RequirePendingInvite(inviteId, userId);

        var joined = await JoinOpenRoomAsync(
            userId,
            new JoinOpenRoomRequest(MatchId: invite.MatchId),
            cancellationToken);

        invite.Status = RoomInviteStatus.Accepted;
        _invites.Update(invite);

        var acceptor = await _userRepository.GetByIdAsync(userId, cancellationToken);
        await _realtime.RoomInviteResolvedAsync(
            invite.FromUserId,
            new RoomInviteResolvedDto(invite.Id, invite.MatchId, true, acceptor?.Username ?? "Player"),
            cancellationToken);

        return joined;
    }

    public async Task DenyWaitingRoomInviteAsync(
        Guid userId,
        Guid inviteId,
        CancellationToken cancellationToken = default)
    {
        var invite = RequirePendingInvite(inviteId, userId);
        invite.Status = RoomInviteStatus.Denied;
        _invites.Update(invite);

        var denier = await _userRepository.GetByIdAsync(userId, cancellationToken);
        await _realtime.RoomInviteResolvedAsync(
            invite.FromUserId,
            new RoomInviteResolvedDto(invite.Id, invite.MatchId, false, denier?.Username ?? "Player"),
            cancellationToken);
    }

    private RoomInvite RequirePendingInvite(Guid inviteId, Guid userId)
    {
        var invite = _invites.Get(inviteId)
            ?? throw new ServiceException("Invite not found.", "inviteErrors.notFound");

        if (invite.ToUserId != userId)
            throw new ServiceException("Invite not found.", "inviteErrors.notFound");

        if (invite.Status != RoomInviteStatus.Pending || invite.ExpiresAt <= DateTime.UtcNow)
            throw new ServiceException("This invite is no longer valid.", "inviteErrors.expired");

        return invite;
    }

    private static bool IsBotUser(User user, Match match) =>
        string.Equals(user.PasswordHash, "BOT", StringComparison.Ordinal)
        || user.Username.StartsWith("Bot_", StringComparison.OrdinalIgnoreCase)
        || match.Players.Any(p => p.UserId == user.Id && p.IsBot);

    private async Task<bool> IsBotInAnyMatchAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await _matchRepository.GetActiveMatchesAsync(cancellationToken);
        return active.Any(m => m.Players.Any(p => p.UserId == userId && p.IsBot));
    }

    private async Task<Match?> ResolveOpenRoomAsync(
        JoinOpenRoomRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MatchId is Guid matchId)
            return await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);

        var code = (request.RoomCode ?? string.Empty).Trim();
        if (code.Length >= 4)
        {
            // Accept pasted full match ids as well as short lobby codes.
            if (Guid.TryParse(code, out var parsedId))
                return await _matchRepository.GetWithPlayersAsync(parsedId, cancellationToken);

            var normalized = code.Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
            if (normalized.Length >= 32 && Guid.TryParseExact(normalized, "N", out var nId))
                return await _matchRepository.GetWithPlayersAsync(nId, cancellationToken);

            var openRooms = await _matchRepository.GetOpenWaitingRoomsAsync(cancellationToken);
            var matches = openRooms
                .Where(m => m.Id.ToString("N").StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 1)
                return matches[0];

            if (matches.Count > 1)
                throw new ServiceException("Multiple rooms match that code. Use the full match id.");

            return null;
        }

        throw new ServiceException("Provide a match id or a room code (at least 4 characters).");
    }

    private async Task EnsureUserNotInAnotherActiveMatchAsync(
        Guid userId,
        Guid joiningMatchId,
        CancellationToken cancellationToken)
    {
        var active = await _matchRepository.GetActiveMatchesAsync(cancellationToken);
        var conflict = active.FirstOrDefault(m =>
            m.Id != joiningMatchId
            && (m.Status == MatchStatus.Waiting || m.Status == MatchStatus.InProgress)
            && m.Players.Any(p => p.UserId == userId && !p.IsBot));

        if (conflict is not null)
            throw new ServiceException("You are already in another match. Leave or finish it first.");
    }

    private OpenRoomDto ToOpenRoomDto(Match match)
    {
        var host = match.Players
            .Where(p => !p.IsBot)
            .OrderBy(p => p.SeatIndex)
            .FirstOrDefault();

        return new OpenRoomDto(
            match.Id,
            string.IsNullOrWhiteSpace(match.Name) ? "Survivor Squad" : match.Name!,
            match.Players.Count,
            match.MaxPlayers,
            match.CreatedAt,
            host?.User?.Username,
            ToRoomCode(match.Id),
            match.FillWithBots,
            _settings.MatchEntryFeeCoins);
    }

    private JoinOpenRoomResponse ToJoinResponse(Match match) =>
        new(
            match.Id,
            match.SessionToken,
            string.IsNullOrWhiteSpace(match.Name) ? "Survivor Squad" : match.Name!,
            match.MaxPlayers,
            match.FillWithBots,
            _settings.MatchEntryFeeCoins,
            ToRoomCode(match.Id));

    private static string ToRoomCode(Guid matchId) =>
        matchId.ToString("N")[..4].ToUpperInvariant();

    public async Task<JoinQueueResponse> JoinQueueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_pendingMatches.TryTake(userId, out var matchId, out var sessionToken))
        {
            return new JoinQueueResponse(false, "Match created.", matchId, sessionToken);
        }

        if (_queue.Contains(userId))
            return new JoinQueueResponse(true, "Already in queue.");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException("User not found.");

        if (!await _coinService.CanAffordEntryFeeAsync(userId, cancellationToken))
            throw new ServiceException($"Insufficient coins. Entry fee is {_settings.MatchEntryFeeCoins} coins.");

        if (!_queue.TryEnqueue(userId))
            return new JoinQueueResponse(true, "Already in queue.");

        var requiredPlayers = _settings.MatchmakingPlayerCount;
        if (_queue.Count < requiredPlayers)
        {
            return new JoinQueueResponse(
                true,
                $"Waiting for players ({_queue.Count}/{requiredPlayers}).");
        }

        await _matchCreationGate.WaitAsync(cancellationToken);
        try
        {
            if (_queue.Count < requiredPlayers)
            {
                return new JoinQueueResponse(
                    true,
                    $"Waiting for players ({_queue.Count}/{requiredPlayers}).");
            }

            return await CreateMatchFromQueueAsync(requiredPlayers, cancellationToken);
        }
        finally
        {
            _matchCreationGate.Release();
        }
    }

    public Task LeaveQueueAsync(Guid userId)
    {
        _queue.TryRemove(userId);
        _pendingMatches.Remove(userId);
        return Task.CompletedTask;
    }

    public async Task ProcessQueueTimeoutsAsync(CancellationToken cancellationToken = default)
    {
        if (_settings.MatchmakingBotFillTimeoutSeconds <= 0
            || !_settings.FillWithBotsWhenUnderCapacity)
            return;

        if (!await _matchCreationGate.WaitAsync(0, cancellationToken))
            return;

        try
        {
            var requiredPlayers = _settings.MatchmakingPlayerCount;
            var queueCount = _queue.Count;
            if (queueCount == 0 || queueCount >= requiredPlayers)
                return;

            var oldest = _queue.GetOldestEnqueueUtc();
            if (oldest is null)
                return;

            var waited = DateTime.UtcNow - oldest.Value;
            if (waited < TimeSpan.FromSeconds(_settings.MatchmakingBotFillTimeoutSeconds))
                return;

            _logger.LogInformation(
                "Matchmaking timeout reached ({Seconds}s). Creating match for {Count} queued player(s) with bots.",
                _settings.MatchmakingBotFillTimeoutSeconds,
                queueCount);

            await CreateMatchFromQueueAsync(queueCount, cancellationToken);
        }
        finally
        {
            _matchCreationGate.Release();
        }
    }

    public async Task ProcessWaitingRoomBotFillsAsync(CancellationToken cancellationToken = default)
    {
        if (_settings.MatchmakingBotFillTimeoutSeconds <= 0)
            return;

        var rooms = await _matchRepository.GetWaitingRoomsNeedingBotFillAsync(
            TimeSpan.FromSeconds(_settings.MatchmakingBotFillTimeoutSeconds),
            cancellationToken);

        foreach (var match in rooms)
        {
            if (match.Players.Count >= match.MaxPlayers)
                continue;

            await _botService.FillMatchWithBotsAsync(match.Id, match.MaxPlayers, cancellationToken);
            await _roomStateMachine.SyncPlayersFromMatchAsync(match.Id, cancellationToken);
            _logger.LogInformation(
                "Filled waiting room {MatchId} with bots up to {MaxPlayers} after {Seconds}s.",
                match.Id,
                match.MaxPlayers,
                _settings.MatchmakingBotFillTimeoutSeconds);
        }
    }

    private async Task<JoinQueueResponse> CreateMatchFromQueueAsync(
        int playerCount,
        CancellationToken cancellationToken)
    {
        if (!_queue.TryDequeueBatch(playerCount, out var selected) || selected.Count == 0)
            return new JoinQueueResponse(true, "Waiting for players.");

        var match = await CreateMatchAsync(selected, cancellationToken);
        StagePendingMatches(selected, match);

        return new JoinQueueResponse(
            false,
            "Match created.",
            match.Id,
            match.SessionToken);
    }

    private void StagePendingMatches(IReadOnlyList<Guid> playerIds, Match match)
    {
        foreach (var playerId in playerIds)
            _pendingMatches.Set(playerId, match.Id, match.SessionToken);
    }

    private async Task<Match> CreateMatchAsync(List<Guid> playerIds, CancellationToken cancellationToken)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.Waiting,
            CurrentPhase = GamePhase.Lobby,
            SessionToken = Guid.NewGuid().ToString("N"),
            Name = $"Match {_settings.MatchmakingPlayerCount}",
            MaxPlayers = _settings.MatchmakingPlayerCount,
            FillWithBots = _settings.FillWithBotsWhenUnderCapacity,
            CreatedAt = DateTime.UtcNow
        };

        for (var i = 0; i < playerIds.Count; i++)
        {
            match.Players.Add(new MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                UserId = playerIds[i],
                SeatIndex = i,
                JoinedAt = DateTime.UtcNow
            });
        }

        await _matchRepository.AddAsync(match, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_settings.FillWithBotsWhenUnderCapacity && match.Players.Count < match.MaxPlayers)
        {
            await _botService.FillMatchWithBotsAsync(match.Id, match.MaxPlayers, cancellationToken);
            _logger.LogInformation(
                "Filled match {MatchId} with bots up to {MaxPlayers} players.",
                match.Id,
                match.MaxPlayers);
        }

        return match;
    }
}
