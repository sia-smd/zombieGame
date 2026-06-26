namespace ZombieGame.Application.Game;

using ZombieGame.Application.Interfaces;

public sealed class NullGameRealtimeNotifier : IGameRealtimeNotifier
{
    public Task BroadcastStateAsync(Guid matchId, object state, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task BroadcastEventAsync(Guid matchId, bool success, string message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
