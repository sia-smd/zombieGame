using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using ZombieGame.UnityClient.Networking.Models;

namespace ZombieGame.UnityClient.Networking
{
    /// <summary>
    /// Live SignalR client for /hubs/game.
    /// Requires: Microsoft.AspNetCore.SignalR.Client
    /// </summary>
    public sealed class GameHubClient : IAsyncDisposable
    {
        private readonly HubConnection _connection;

        public event Action<GameStateDto>? OnSyncState;
        public event Action<GameEventDto>? OnGameEvent;
        public event Action<Exception>? OnConnectionClosed;
        public event Action<string?>? OnReconnecting;
        public event Action<string?>? OnReconnected;

        public HubConnectionState State => _connection.State;

        public GameHubClient(string baseUrl, string accessToken)
        {
            var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/game?access_token={Uri.EscapeDataString(accessToken)}";

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<GameStateDto>("SyncState", state => OnSyncState?.Invoke(state));
            _connection.On<GameEventDto>("GameEvent", evt => OnGameEvent?.Invoke(evt));
            _connection.Closed += ex =>
            {
                OnConnectionClosed?.Invoke(ex ?? new Exception("Hub connection closed."));
                return Task.CompletedTask;
            };
            _connection.Reconnecting += error =>
            {
                OnReconnecting?.Invoke(error?.Message);
                return Task.CompletedTask;
            };
            _connection.Reconnected += connectionId =>
            {
                OnReconnected?.Invoke(connectionId);
                return Task.CompletedTask;
            };
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            if (_connection.State == HubConnectionState.Connected)
                return;

            await _connection.StartAsync(ct);
        }

        public Task JoinMatchAsync(Guid matchId, string sessionToken, CancellationToken ct = default) =>
            _connection.InvokeAsync("JoinMatch", matchId, sessionToken, ct);

        public Task StartGameAsync(Guid matchId, string sessionToken, CancellationToken ct = default) =>
            _connection.InvokeAsync("StartGame", matchId, sessionToken, ct);

        public Task PlayCardAsync(Guid matchId, string sessionToken, PlayCardRequest request, CancellationToken ct = default) =>
            _connection.InvokeAsync("PlayCard", matchId, sessionToken, request, ct);

        public Task PassActionAsync(Guid matchId, string sessionToken, PassActionRequest request, CancellationToken ct = default) =>
            _connection.InvokeAsync("PassAction", matchId, sessionToken, request, ct);

        public Task EndTurnAsync(Guid matchId, string sessionToken, EndTurnRequest request, CancellationToken ct = default) =>
            _connection.InvokeAsync("EndTurn", matchId, sessionToken, request, ct);

        public Task VotePlayerAsync(Guid matchId, string sessionToken, VotePlayerRequest request, CancellationToken ct = default) =>
            _connection.InvokeAsync("VotePlayer", matchId, sessionToken, request, ct);

        public Task SyncStateAsync(Guid matchId, string sessionToken, CancellationToken ct = default) =>
            _connection.InvokeAsync("SyncState", matchId, sessionToken, ct);

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
