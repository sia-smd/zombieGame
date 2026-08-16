namespace ZombieGame.Application.Services;

using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Room;

public sealed class ActiveMatchQueryService : IActiveMatchQueryService
{
    private readonly IPlayerReconnectService _reconnect;

    public ActiveMatchQueryService(IPlayerReconnectService reconnect) => _reconnect = reconnect;

    public async Task<ActiveMatchResponse> GetActiveMatchForPlayerAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _reconnect.GetActiveMatchAsync(userId, cancellationToken)
        ?? new ActiveMatchResponse(false);
}
