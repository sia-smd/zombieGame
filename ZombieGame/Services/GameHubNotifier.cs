namespace ZombieGame.Api.Services;

using Microsoft.AspNetCore.SignalR;
using ZombieGame.Api.Hubs;
using ZombieGame.Application.Interfaces;

public sealed class GameHubNotifier : IGameRealtimeNotifier
{
    private readonly IHubContext<GameHub> _hub;

    public GameHubNotifier(IHubContext<GameHub> hub) => _hub = hub;

    public Task BroadcastStateAsync(Guid matchId, object state, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(MatchGroup(matchId)).SendAsync("SyncState", state, cancellationToken);

    public Task BroadcastEventAsync(Guid matchId, bool success, string message, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(MatchGroup(matchId)).SendAsync("GameEvent", new { success, message }, cancellationToken);

    private static string MatchGroup(Guid matchId) => $"match-{matchId}";
}
