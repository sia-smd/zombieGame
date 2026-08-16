namespace ZombieGame.Application.Room;

using Microsoft.Extensions.Logging;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Room.Bots;
using ZombieGame.Domain.Interfaces;

public sealed class RoomLoopProcessor : IRoomLoopProcessor
{
    private readonly IActiveMatchRegistry _activeMatches;
    private readonly IRoomStateMachine _stateMachine;
    private readonly IRoomBotExecutor _roomBots;
    private readonly ILogger<RoomLoopProcessor> _logger;

    public RoomLoopProcessor(
        IActiveMatchRegistry activeMatches,
        IRoomStateMachine stateMachine,
        IRoomBotExecutor roomBots,
        ILogger<RoomLoopProcessor> logger)
    {
        _activeMatches = activeMatches;
        _stateMachine = stateMachine;
        _roomBots = roomBots;
        _logger = logger;
    }

    public async Task ProcessActiveRoomsAsync(CancellationToken cancellationToken = default)
    {
        var matchIds = await _activeMatches.GetActiveMatchIdsAsync(cancellationToken);
        foreach (var matchId in matchIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var phase = "unknown";
            try
            {
                phase = (await _stateMachine.GetStateAsync(matchId, cancellationToken))
                    ?.CurrentPhase.ToString() ?? "missing";
                await _roomBots.ProcessRoomBotsAsync(matchId, cancellationToken);
                await _stateMachine.TickAsync(matchId, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Room loop failed for {MatchId} in phase {Phase}. The room will be retried.",
                    matchId,
                    phase);
            }
        }
    }
}
