namespace ZombieGame.Application.Services;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Matchmaking;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public class MatchmakingService : IMatchmakingService
{
    private readonly IMatchmakingQueue _queue;
    private readonly IMatchRepository _matchRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICoinService _coinService;
    private readonly IBotService _botService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GameSettings _settings;

    public MatchmakingService(
        IMatchmakingQueue queue,
        IMatchRepository matchRepository,
        IUserRepository userRepository,
        ICoinService coinService,
        IBotService botService,
        IUnitOfWork unitOfWork,
        IOptions<GameSettings> settings)
    {
        _queue = queue;
        _matchRepository = matchRepository;
        _userRepository = userRepository;
        _coinService = coinService;
        _botService = botService;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
    }

    public int QueueCount => _queue.Count;

    public bool IsInQueue(Guid userId) => _queue.Contains(userId);

    public async Task<JoinQueueResponse> JoinQueueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
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
            return new JoinQueueResponse(true, $"Waiting for players ({_queue.Count}/{requiredPlayers}).");

        if (!_queue.TryDequeueBatch(requiredPlayers, out var selected))
            return new JoinQueueResponse(true, "Waiting for players.");

        var match = await CreateMatchAsync(selected, cancellationToken);
        return new JoinQueueResponse(
            false,
            "Match created.",
            match.Id,
            match.SessionToken);
    }

    public Task LeaveQueueAsync(Guid userId)
    {
        _queue.TryRemove(userId);
        return Task.CompletedTask;
    }

    private async Task<Match> CreateMatchAsync(List<Guid> playerIds, CancellationToken cancellationToken)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.Waiting,
            CurrentPhase = GamePhase.Lobby,
            SessionToken = Guid.NewGuid().ToString("N"),
            MaxPlayers = _settings.MatchmakingPlayerCount,
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
            await _botService.FillMatchWithBotsAsync(match.Id, match.MaxPlayers, cancellationToken);

        return match;
    }
}
