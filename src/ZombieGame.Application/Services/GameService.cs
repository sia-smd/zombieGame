namespace ZombieGame.Application.Services;



using System.Text.Json;

using Microsoft.Extensions.Options;

using ZombieGame.Application.Common;

using ZombieGame.Application.DTOs.Game;

using ZombieGame.Application.Bots.Cognition;

using ZombieGame.Application.Game;

using ZombieGame.Application.GameRules;

using ZombieGame.Application.Interfaces;

using ZombieGame.Application.Options;

using ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

using ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Models;



public class GameService : IGameService

{

    private readonly IMatchRepository _matchRepository;

    private readonly IUserRepository _userRepository;

    private readonly IGameActionLogRepository _actionLogRepository;

    private readonly IGameSessionStore _sessionStore;

    private readonly IGameSessionRecoveryService _recovery;

    private readonly IActiveMatchRegistry _activeMatches;

    private readonly ICoinService _coinService;

    private readonly ICardRegistry _cardRegistry;

    private readonly IGameRulesEngine _rulesEngine;

    private readonly ICardConsumptionService _consumption;

    private readonly IUnitOfWork _unitOfWork;

    private readonly IRoomLock _roomLock;

    private readonly RoomSettings _roomSettings;

    private readonly GameSettings _settings;



    public GameService(

        IMatchRepository matchRepository,

        IUserRepository userRepository,

        IGameActionLogRepository actionLogRepository,

        IGameSessionStore sessionStore,

        IGameSessionRecoveryService recovery,

        IActiveMatchRegistry activeMatches,

        ICoinService coinService,

        ICardRegistry cardRegistry,

        IGameRulesEngine rulesEngine,

        ICardConsumptionService consumption,

        IUnitOfWork unitOfWork,

        IRoomLock roomLock,

        IOptions<RoomSettings> roomSettings,

        IOptions<GameSettings> settings)

    {

        _matchRepository = matchRepository;

        _userRepository = userRepository;

        _actionLogRepository = actionLogRepository;

        _sessionStore = sessionStore;

        _recovery = recovery;

        _activeMatches = activeMatches;

        _coinService = coinService;

        _cardRegistry = cardRegistry;

        _rulesEngine = rulesEngine;

        _consumption = consumption;

        _unitOfWork = unitOfWork;

        _roomLock = roomLock;

        _roomSettings = roomSettings.Value;

        _settings = settings.Value;

    }



    public async Task<GameActionResult> JoinMatchAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default)

    {

        var match = await ValidateMatchAccessAsync(userId, matchId, sessionToken, cancellationToken);



        var state = await _sessionStore.GetAsync(matchId, cancellationToken)

            ?? await _recovery.TryRecoverAsync(matchId, cancellationToken);



        if (state is null)

        {

            if (match.Status != MatchStatus.Waiting)

                throw new ServiceException("Game session is not available. Please retry in a moment.");



            state = await InitializeSessionAsync(match, cancellationToken);

        }



        return new GameActionResult(true, "Joined match.", GameStateMapper.ToDto(state, includeHands: true));

    }



    public async Task<GameActionResult> StartGameAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default)

    {

        var match = await ValidateMatchAccessAsync(userId, matchId, sessionToken, cancellationToken);

        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);

        if (match.Status == MatchStatus.InProgress)
        {
            var existingState = await GetRequiredStateAsync(matchId, match, cancellationToken);
            return new GameActionResult(
                true,
                "Game already started.",
                GameStateMapper.ToDto(existingState, includeHands: true));
        }

        if (match.Status != MatchStatus.Waiting)

            throw new ServiceException("Cannot start this match.");



        match.Status = MatchStatus.InProgress;

        match.StartedAt = DateTime.UtcNow;

        match.CurrentPhase = GamePhase.Day;

        _matchRepository.Update(match);



        var state = await GetOrCreateStateAsync(match, cancellationToken);

        _rulesEngine.StartMatch(state, match);

        await _coinService.CollectMatchEntryFeesAsync(
            matchId,
            state.Players.Where(p => !p.IsBot).Select(p => p.UserId),
            cancellationToken);

        BotObservationRecorder.InitializeBots(state, Random.Shared);



        SyncMatchPlayersFromState(match, state);

        await PersistSessionAsync(state, cancellationToken);

        await _activeMatches.RegisterAsync(matchId, cancellationToken);



        await LogActionAsync(matchId, userId, "StartGame", JsonSerializer.Serialize(new

        {

            dayEvent = state.CurrentDayEvent.ToString(),

            turn = state.TurnNumber

        }), null, state, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);



        return new GameActionResult(true, "Game started.", GameStateMapper.ToDto(state, includeHands: true));

    }



    public async Task<GameActionResult> PlayCardAsync(Guid userId, Guid matchId, string sessionToken, PlayCardRequest request, CancellationToken cancellationToken = default)

    {

        var match = await ValidateMatchAccessAsync(userId, matchId, sessionToken, cancellationToken);

        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);

        await EnsureIdempotentAsync(matchId, request.IdempotencyKey, cancellationToken);



        var state = await GetRequiredStateAsync(matchId, match, cancellationToken);

        var card = _cardRegistry.GetById(request.CardId)

            ?? throw new ServiceException("Invalid card.");



        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == userId)

            ?? throw new ServiceException("Player hand not found.");



        if (!hand.ContainsCard(request.CardId))

            throw new ServiceException("Card not in hand.");



        var targetId = request.TargetUserId ?? userId;

        var effectResult = _rulesEngine.PlayCard(state, userId, card, targetId);



        _consumption.ConsumeAfterPlay(hand, card);

        BotObservationRecorder.OnCardOutcome(state, userId, targetId, effectResult, card.EffectKey);

        SyncMatchPlayersFromState(match, state);

        await PersistSessionAsync(state, cancellationToken);



        await LogActionAsync(matchId, userId, "PlayCard", JsonSerializer.Serialize(new

        {

            request.CardId,

            request.TargetUserId,

            effectResult.Message

        }), request.IdempotencyKey, state, cancellationToken);



        if (effectResult.FriendlyFire)

        {

            await LogActionAsync(matchId, userId, "FriendlyFire", JsonSerializer.Serialize(new

            {

                request.TargetUserId,

                state.FriendlyFireCount

            }), $"{request.IdempotencyKey}-ff", state, cancellationToken);

        }



        var winTeam = _rulesEngine.EvaluateImmediateWin(state);

        if (winTeam is not null)

        {

            await _rulesEngine.CompleteMatchIfWonAsync(state, match, cancellationToken);

            await OnMatchFinishedAsync(matchId, state, cancellationToken);

        }



        await _unitOfWork.SaveChangesAsync(cancellationToken);



        return new GameActionResult(true, effectResult.Message, GameStateMapper.ToDto(state, includeHands: true));

    }



    public async Task<GameActionResult> PassActionAsync(Guid userId, Guid matchId, string sessionToken, PassActionRequest request, CancellationToken cancellationToken = default)

    {

        var match = await ValidateMatchAccessAsync(userId, matchId, sessionToken, cancellationToken);

        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);

        await EnsureIdempotentAsync(matchId, request.IdempotencyKey, cancellationToken);



        var state = await GetRequiredStateAsync(matchId, match, cancellationToken);

        _rulesEngine.PassAction(state, userId);

        BotObservationRecorder.OnPass(state, userId);

        await PersistSessionAsync(state, cancellationToken);



        await LogActionAsync(matchId, userId, "Pass", """{"action":"pass"}""", request.IdempotencyKey, state, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);



        return new GameActionResult(true, "Pass recorded.", GameStateMapper.ToDto(state, includeHands: true));

    }



    public async Task<GameActionResult> EndTurnAsync(Guid userId, Guid matchId, string sessionToken, EndTurnRequest request, CancellationToken cancellationToken = default)

    {

        var match = await ValidateMatchAccessAsync(userId, matchId, sessionToken, cancellationToken);

        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);

        await EnsureIdempotentAsync(matchId, request.IdempotencyKey, cancellationToken);



        var state = await GetRequiredStateAsync(matchId, match, cancellationToken);

        BotObservationRecorder.OnDayEnd(state);

        _rulesEngine.EndDayPhase(state, _settings);



        match.CurrentPhase = state.CurrentPhase;

        _matchRepository.Update(match);

        await PersistSessionAsync(state, cancellationToken);



        await LogActionAsync(matchId, userId, "EndTurn", "{}", request.IdempotencyKey, state, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);



        return new GameActionResult(true, "Entering discussion phase.", GameStateMapper.ToDto(state));

    }



    public async Task<GameActionResult> VotePlayerAsync(Guid userId, Guid matchId, string sessionToken, VotePlayerRequest request, CancellationToken cancellationToken = default)

    {

        var match = await ValidateMatchAccessAsync(userId, matchId, sessionToken, cancellationToken);

        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);

        await EnsureIdempotentAsync(matchId, request.IdempotencyKey, cancellationToken);



        var state = await GetRequiredStateAsync(matchId, match, cancellationToken);

        _rulesEngine.CastVote(state, userId, request.TargetUserId);

        await PersistSessionAsync(state, cancellationToken);



        await LogActionAsync(matchId, userId, "VotePlayer", JsonSerializer.Serialize(request), request.IdempotencyKey, state, cancellationToken);



        if (_rulesEngine.TryAdvanceVotingToResolution(state, _settings))

        {

            match.CurrentPhase = GamePhase.Resolution;

            _matchRepository.Update(match);

            await _rulesEngine.ProcessResolutionAsync(state, match, cancellationToken);

            if (state.CurrentPhase == GamePhase.Day)

                BotObservationRecorder.OnDayStart(state);

            await PersistSessionAsync(state, cancellationToken);



            if (state.IsFinished)

                await OnMatchFinishedAsync(matchId, state, cancellationToken);

        }



        await _unitOfWork.SaveChangesAsync(cancellationToken);



        return new GameActionResult(true, "Vote recorded.", GameStateMapper.ToDto(state));

    }



    public async Task<GameStateDto?> GetStateAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default)

    {

        await ValidateMatchAccessAsync(userId, matchId, sessionToken, cancellationToken);

        var state = await _sessionStore.GetAsync(matchId, cancellationToken)

            ?? await _recovery.TryRecoverAsync(matchId, cancellationToken);

        return state is null ? null : GameStateMapper.ToDto(state, includeHands: true);

    }



    public async Task<GameActionResult> AdvancePhaseIfExpiredAsync(Guid matchId, CancellationToken cancellationToken = default)

    {

        await using var guard = await AcquireRequiredLockAsync(matchId, cancellationToken);

        var state = await _sessionStore.GetAsync(matchId, cancellationToken)

            ?? await _recovery.TryRecoverAsync(matchId, cancellationToken);

        if (state is null || state.IsFinished)

            return new GameActionResult(false, "No phase transition needed.");



        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken)

            ?? throw new ServiceException("Match not found.");



        if (state.CurrentPhase == GamePhase.Discussion)

        {

            var result = _rulesEngine.AdvancePhase(state, match, _settings);

            if (!result.Advanced)

                return new GameActionResult(false, result.Message);



            match.CurrentPhase = state.CurrentPhase;

            _matchRepository.Update(match);

            await PersistSessionAsync(state, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new GameActionResult(true, result.Message, GameStateMapper.ToDto(state));

        }



        if (state.CurrentPhase == GamePhase.Voting &&

            _rulesEngine.TryAdvanceVotingToResolution(state, _settings))

        {

            match.CurrentPhase = GamePhase.Resolution;

            _matchRepository.Update(match);

            await _rulesEngine.ProcessResolutionAsync(state, match, cancellationToken);

            if (state.CurrentPhase == GamePhase.Day)

                BotObservationRecorder.OnDayStart(state);

            await PersistSessionAsync(state, cancellationToken);



            if (state.IsFinished)

                await OnMatchFinishedAsync(matchId, state, cancellationToken);



            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new GameActionResult(true, "Voting closed. Resolution processed.", GameStateMapper.ToDto(state));

        }



        return new GameActionResult(false, "No phase transition needed.");

    }



    private async Task<GameSessionState> GetRequiredStateAsync(Guid matchId, Match match, CancellationToken cancellationToken) =>

        await _sessionStore.GetAsync(matchId, cancellationToken)

        ?? await _recovery.TryRecoverAsync(matchId, cancellationToken)

        ?? await GetOrCreateStateAsync(match, cancellationToken);



    private async Task<GameSessionState> GetOrCreateStateAsync(Match match, CancellationToken cancellationToken)

    {

        var existing = await _sessionStore.GetAsync(match.Id, cancellationToken);

        if (existing is not null)

            return existing;



        return await InitializeSessionAsync(match, cancellationToken);

    }



    private async Task PersistSessionAsync(GameSessionState state, CancellationToken cancellationToken) =>

        await _sessionStore.SetAsync(state, cancellationToken);

    private async Task<IAsyncDisposable> AcquireRequiredLockAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var lease = TimeSpan.FromSeconds(Math.Max(1, _roomSettings.LockTtlSeconds));
        var wait = TimeSpan.FromMilliseconds(Math.Max(0, _roomSettings.LockWaitMilliseconds));
        var deadline = DateTime.UtcNow + wait;

        while (true)
        {
            var handle = await _roomLock.TryAcquireAsync(matchId, lease, cancellationToken);
            if (handle is not null)
                return handle;

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                throw new ServiceException("Match is busy. Try again.");

            var delay = remaining < TimeSpan.FromMilliseconds(20) ? remaining : TimeSpan.FromMilliseconds(20);
            await Task.Delay(delay, cancellationToken);
        }
    }



    private async Task OnMatchFinishedAsync(Guid matchId, GameSessionState state, CancellationToken cancellationToken)

    {

        await _activeMatches.UnregisterAsync(matchId, cancellationToken);

        var actorId = state.Players.FirstOrDefault()?.UserId ?? Guid.Empty;
        if (actorId == Guid.Empty)
            return;

        await LogActionAsync(matchId, actorId, "MatchFinished", JsonSerializer.Serialize(new
        {
            winner = state.WinTeam.ToString()
        }), null, state, cancellationToken);

    }



    private async Task<Match> ValidateMatchAccessAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken)

    {

        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken)

            ?? throw new ServiceException("Match not found.");



        if (!string.Equals(match.SessionToken, sessionToken, StringComparison.Ordinal))

            throw new ServiceException("Invalid session token.");



        if (!match.Players.Any(p => p.UserId == userId))

            throw new ServiceException("User is not in this match.");



        return match;

    }



    private async Task<GameSessionState> InitializeSessionAsync(Match match, CancellationToken cancellationToken)

    {

        var players = new List<GamePlayerState>();

        foreach (var mp in match.Players.OrderBy(p => p.SeatIndex))

        {

            var user = await _userRepository.GetByIdAsync(mp.UserId, cancellationToken);

            players.Add(new GamePlayerState

            {

                UserId = mp.UserId,

                Username = user?.Username ?? "Unknown",

                IsBot = mp.IsBot,

                IsAlive = mp.IsAlive,

                SeatIndex = mp.SeatIndex,

                Role = mp.Role

            });

        }



        var state = new GameSessionState

        {

            MatchId = match.Id,

            SessionToken = match.SessionToken,

            CurrentPhase = match.CurrentPhase,

            Players = players,

            PlayerHands = players.Select(p => new PlayerCardState { UserId = p.UserId }).ToList()

        };



        if (!await _sessionStore.TryAddAsync(match.Id, state, cancellationToken))

        {

            var existing = await _sessionStore.GetAsync(match.Id, cancellationToken);

            if (existing is not null)

                return existing;

        }



        return state;

    }



    private static void SyncMatchPlayersFromState(Match match, GameSessionState state)

    {

        foreach (var sp in state.Players)

        {

            var mp = match.Players.FirstOrDefault(p => p.UserId == sp.UserId);

            if (mp is null) continue;

            mp.Role = sp.Role;

            mp.IsAlive = sp.IsAlive;

        }

    }



    private async Task EnsureIdempotentAsync(Guid matchId, string idempotencyKey, CancellationToken cancellationToken)

    {

        if (string.IsNullOrWhiteSpace(idempotencyKey))

            throw new ServiceException("Idempotency key is required.");



        if (await _actionLogRepository.ExistsByIdempotencyKeyAsync(matchId, idempotencyKey, cancellationToken))

            throw new ServiceException("Duplicate action detected.");

    }



    private async Task LogActionAsync(

        Guid matchId,

        Guid userId,

        string actionType,

        string payload,

        string? idempotencyKey,

        GameSessionState? snapshot,

        CancellationToken cancellationToken)

    {

        await _actionLogRepository.AddAsync(new GameActionLog

        {

            Id = Guid.NewGuid(),

            MatchId = matchId,

            UserId = userId,

            ActionType = actionType,

            PayloadJson = payload,

            IdempotencyKey = idempotencyKey,

            StateSnapshotJson = snapshot is not null ? GameSessionStateSerializer.Serialize(snapshot) : null,

            CreatedAt = DateTime.UtcNow

        }, cancellationToken);

    }

}


