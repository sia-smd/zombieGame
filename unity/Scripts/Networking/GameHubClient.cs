using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZombieGame.UnityClient.Networking
{
    /// <summary>
    /// SignalR hub client stub. Requires Microsoft.AspNetCore.SignalR.Client in Unity.
    /// </summary>
    public sealed class GameHubClient : IAsyncDisposable
    {
        private readonly string _hubUrl;
        private readonly string _accessToken;
        // private HubConnection? _connection;

        public event Action<object>? OnSyncState;
        public event Action<object>? OnGameEvent;

        public GameHubClient(string baseUrl, string accessToken)
        {
            _hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/game?access_token={accessToken}";
            _accessToken = accessToken;
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            // TODO: Initialize HubConnectionBuilder
            // _connection = new HubConnectionBuilder()
            //     .WithUrl(_hubUrl)
            //     .WithAutomaticReconnect()
            //     .Build();
            // _connection.On<object>("SyncState", state => OnSyncState?.Invoke(state));
            // _connection.On<object>("GameEvent", evt => OnGameEvent?.Invoke(evt));
            // return _connection.StartAsync(ct);
            return Task.CompletedTask;
        }

        public Task JoinMatchAsync(Guid matchId, string sessionToken, CancellationToken ct = default)
        {
            // return _connection!.InvokeAsync("JoinMatch", matchId, sessionToken, ct);
            return Task.CompletedTask;
        }

        public Task StartGameAsync(Guid matchId, string sessionToken, CancellationToken ct = default)
        {
            // return _connection!.InvokeAsync("StartGame", matchId, sessionToken, ct);
            return Task.CompletedTask;
        }

        public Task PlayCardAsync(Guid matchId, string sessionToken, Guid cardId, Guid? targetUserId, string idempotencyKey, CancellationToken ct = default)
        {
            // return _connection!.InvokeAsync("PlayCard", matchId, sessionToken, new { cardId, targetUserId, idempotencyKey }, ct);
            return Task.CompletedTask;
        }

        public Task SyncStateAsync(Guid matchId, string sessionToken, CancellationToken ct = default)
        {
            // return _connection!.InvokeAsync("SyncState", matchId, sessionToken, ct);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            // if (_connection is not null) return _connection.DisposeAsync();
            return ValueTask.CompletedTask;
        }
    }
}
