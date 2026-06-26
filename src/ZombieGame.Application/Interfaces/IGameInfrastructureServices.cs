namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.DTOs.Game;
using ZombieGame.Domain.Models;

public interface IGameSessionRecoveryService
{
    Task<GameSessionState?> TryRecoverAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public interface IGameRealtimeNotifier
{
    Task BroadcastStateAsync(Guid matchId, object state, CancellationToken cancellationToken = default);
    Task BroadcastEventAsync(Guid matchId, bool success, string message, CancellationToken cancellationToken = default);
}

public interface IGameLoopProcessor
{
    Task ProcessActiveMatchesAsync(CancellationToken cancellationToken = default);
}

public interface IGameBotExecutor
{
    Task<GameActionResult?> ExecuteNextBotActionAsync(
        Guid matchId,
        Guid botUserId,
        CancellationToken cancellationToken = default);
}
