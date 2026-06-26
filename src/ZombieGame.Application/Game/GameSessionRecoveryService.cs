namespace ZombieGame.Application.Game;

using Microsoft.Extensions.Logging;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public sealed class GameSessionRecoveryService : IGameSessionRecoveryService
{
    private readonly IGameActionLogRepository _actionLogRepository;
    private readonly IGameSessionStore _sessionStore;
    private readonly ILogger<GameSessionRecoveryService> _logger;

    public GameSessionRecoveryService(
        IGameActionLogRepository actionLogRepository,
        IGameSessionStore sessionStore,
        ILogger<GameSessionRecoveryService> logger)
    {
        _actionLogRepository = actionLogRepository;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task<GameSessionState?> TryRecoverAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var latest = await _actionLogRepository.GetLatestWithSnapshotAsync(matchId, cancellationToken);
        if (latest?.StateSnapshotJson is null)
            return null;

        try
        {
            var state = GameSessionStateSerializer.Deserialize(latest.StateSnapshotJson);
            await _sessionStore.SetAsync(state, cancellationToken);
            _logger.LogInformation("Recovered match {MatchId} session from action log snapshot at {CreatedAt}", matchId, latest.CreatedAt);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover match {MatchId} from action log snapshot", matchId);
            return null;
        }
    }
}
