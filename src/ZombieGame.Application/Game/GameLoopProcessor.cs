namespace ZombieGame.Application.Game;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public sealed class GameLoopProcessor : IGameLoopProcessor
{
    private readonly IActiveMatchRegistry _activeMatches;
    private readonly IGameSessionStore _sessionStore;
    private readonly IMatchRepository _matchRepository;
    private readonly IGameService _gameService;
    private readonly IGameBotExecutor _botExecutor;
    private readonly IGameRealtimeNotifier _notifier;
    private readonly IGameSessionRecoveryService _recovery;
    private readonly GameSettings _settings;
    private readonly ILogger<GameLoopProcessor> _logger;

    public GameLoopProcessor(
        IActiveMatchRegistry activeMatches,
        IGameSessionStore sessionStore,
        IMatchRepository matchRepository,
        IGameService gameService,
        IGameBotExecutor botExecutor,
        IGameRealtimeNotifier notifier,
        IGameSessionRecoveryService recovery,
        IOptions<GameSettings> settings,
        ILogger<GameLoopProcessor> logger)
    {
        _activeMatches = activeMatches;
        _sessionStore = sessionStore;
        _matchRepository = matchRepository;
        _gameService = gameService;
        _botExecutor = botExecutor;
        _notifier = notifier;
        _recovery = recovery;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ProcessActiveMatchesAsync(CancellationToken cancellationToken = default)
    {
        var matchIds = await _activeMatches.GetActiveMatchIdsAsync(cancellationToken);
        foreach (var matchId in matchIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await ProcessMatchAsync(matchId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game loop failed for match {MatchId}", matchId);
            }
        }
    }

    private async Task ProcessMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);
        if (match is null || match.Status != MatchStatus.InProgress)
        {
            await _activeMatches.UnregisterAsync(matchId, cancellationToken);
            return;
        }

        var state = await _sessionStore.GetAsync(matchId, cancellationToken)
            ?? await _recovery.TryRecoverAsync(matchId, cancellationToken);

        if (state is null || state.IsFinished)
        {
            if (state?.IsFinished == true)
                await _activeMatches.UnregisterAsync(matchId, cancellationToken);
            return;
        }

        var advanced = await _gameService.AdvancePhaseIfExpiredAsync(matchId, cancellationToken);
        if (advanced.Success && advanced.State is not null)
            await _notifier.BroadcastStateAsync(matchId, advanced.State, cancellationToken);

        state = await _sessionStore.GetAsync(matchId, cancellationToken) ?? state;

        if (state.CurrentPhase is GamePhase.Day or GamePhase.Voting)
        {
            foreach (var bot in state.Players.Where(p => p.IsBot && p.IsAlive))
            {
                if (!ShouldBotAct(state, bot))
                    continue;

                var result = await _botExecutor.ExecuteNextBotActionAsync(matchId, bot.UserId, cancellationToken);
                if (result?.State is not null)
                {
                    await _notifier.BroadcastStateAsync(matchId, result.State, cancellationToken);
                    await _notifier.BroadcastEventAsync(matchId, result.Success, result.Message, cancellationToken);
                }

                if (result is not null)
                    break;
            }
        }

        state = await _sessionStore.GetAsync(matchId, cancellationToken);
        if (state?.IsFinished == true)
            await _activeMatches.UnregisterAsync(matchId, cancellationToken);
    }

    private static bool ShouldBotAct(GameSessionState state, GamePlayerState bot) =>
        state.CurrentPhase switch
        {
            GamePhase.Day => bot.RemainingActions > 0,
            GamePhase.Voting => !state.Votes.ContainsKey(bot.UserId),
            _ => false
        };
}
