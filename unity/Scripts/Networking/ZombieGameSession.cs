using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using ZombieGame.UnityClient.Networking.Models;

namespace ZombieGame.UnityClient.Networking
{
    /// <summary>
    /// High-level client session: guest auth, matchmaking, and gameplay hub.
    /// Use from UI code or <see cref="ZombieGameClientBehaviour"/>.
    /// </summary>
    public sealed class ZombieGameSession : IAsyncDisposable
    {
        private readonly ZombieGameApiClient _api;
        private GameHubClient? _hub;

        public string BaseUrl { get; }
        public Guid? PlayerId { get; private set; }
        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public Guid? MatchId { get; private set; }
        public string? SessionToken { get; private set; }
        public GameStateDto? CurrentState { get; private set; }
        public CurrentPlayerProfileResponse? Profile { get; private set; }

        public event Action<GameStateDto>? StateChanged;
        public event Action<GameEventDto>? GameEventReceived;
        public event Action<string>? StatusMessage;
        public event Action<Exception>? Error;

        public ZombieGameSession(string baseUrl)
        {
            BaseUrl = baseUrl.TrimEnd('/');
            _api = new ZombieGameApiClient(BaseUrl);
        }

        public Task RegisterGuestAsync(CancellationToken ct = default) =>
            RegisterGuestAsync(
                Core.DeviceInfoProvider.DeviceId,
                Core.DeviceInfoProvider.Platform,
                Core.DeviceInfoProvider.AppVersion,
                ct);

        public async Task RegisterGuestAsync(
            string deviceId,
            DevicePlatform platform,
            string appVersion,
            CancellationToken ct = default)
        {
            var result = await _api.RegisterGuestAsync(
                new RegisterGuestRequest(deviceId, platform, appVersion), ct);

            if (result is null)
                throw new InvalidOperationException("Guest registration returned empty response.");

            ApplyTokens(result.PlayerId, result.AccessToken, result.RefreshToken);
            Profile = MapGuestProfile(result);
            StatusMessage?.Invoke($"Welcome {result.Profile.Name}");
        }

        public void RestoreSession(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            _api.SetAccessToken(accessToken);
        }

        public async Task<bool> TryRefreshTokenAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(RefreshToken))
                return false;

            var result = await _api.RefreshTokenAsync(new RefreshTokenRequest(RefreshToken), ct);
            if (result is null)
                return false;

            AccessToken = result.AccessToken;
            RefreshToken = result.RefreshToken;
            _api.SetAccessToken(result.AccessToken);
            return true;
        }

        public async Task LoadProfileAsync(CancellationToken ct = default)
        {
            Profile = await _api.GetProfileMeAsync(ct);
        }

        public async Task<JoinQueueResponse> JoinQueueAsync(CancellationToken ct = default)
        {
            var result = await _api.JoinQueueAsync(ct)
                ?? throw new InvalidOperationException("Join queue returned empty response.");

            ApplyQueueResult(result);
            return result;
        }

        /// <summary>Polls queue/join until a match is created or timeout.</summary>
        public async Task<JoinQueueResponse> JoinQueueAndWaitAsync(
            TimeSpan pollInterval,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            var deadline = DateTime.UtcNow + timeout;
            JoinQueueResponse? last = null;

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                last = await JoinQueueAsync(ct);
                if (!last.Queued && last.MatchId is not null)
                    return last;

                await Task.Delay(pollInterval, ct);
            }

            throw new TimeoutException(
                last is null ? "Matchmaking timed out." : $"Matchmaking timed out: {last.Message}");
        }

        public async Task ConnectHubAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await DisposeHubAsync();

            _hub = new GameHubClient(BaseUrl, AccessToken!);
            _hub.OnSyncState += HandleSyncState;
            _hub.OnGameEvent += evt => GameEventReceived?.Invoke(evt);
            _hub.OnConnectionClosed += ex => Error?.Invoke(ex);
            _hub.OnReconnected += _ => StatusMessage?.Invoke("Reconnected to game server.");

            await _hub.ConnectAsync(ct);
            StatusMessage?.Invoke("Connected to game hub.");
        }

        public async Task JoinMatchAsync(CancellationToken ct = default)
        {
            EnsureHub();
            EnsureMatch();
            await _hub!.JoinMatchAsync(MatchId!.Value, SessionToken!, ct);
        }

        public async Task RejoinMatchAsync(Guid matchId, string sessionToken, CancellationToken ct = default)
        {
            MatchId = matchId;
            SessionToken = sessionToken;
            await ConnectHubAsync(ct);
            await JoinMatchAsync(ct);
        }

        public Task StartGameAsync(CancellationToken ct = default)
        {
            EnsureHub();
            EnsureMatch();
            return _hub!.StartGameAsync(MatchId!.Value, SessionToken!, ct);
        }

        public Task PassAsync(CancellationToken ct = default)
        {
            EnsureHub();
            EnsureMatch();
            return _hub!.PassActionAsync(
                MatchId!.Value,
                SessionToken!,
                new PassActionRequest(NewIdempotencyKey()),
                ct);
        }

        public Task PlayCardAsync(Guid cardId, Guid? targetUserId, CancellationToken ct = default)
        {
            EnsureHub();
            EnsureMatch();
            return _hub!.PlayCardAsync(
                MatchId!.Value,
                SessionToken!,
                new PlayCardRequest(cardId, targetUserId, NewIdempotencyKey()),
                ct);
        }

        public Task EndTurnAsync(CancellationToken ct = default)
        {
            EnsureHub();
            EnsureMatch();
            return _hub!.EndTurnAsync(
                MatchId!.Value,
                SessionToken!,
                new EndTurnRequest(NewIdempotencyKey()),
                ct);
        }

        public Task VoteAsync(Guid targetUserId, CancellationToken ct = default)
        {
            EnsureHub();
            EnsureMatch();
            return _hub!.VotePlayerAsync(
                MatchId!.Value,
                SessionToken!,
                new VotePlayerRequest(targetUserId, NewIdempotencyKey()),
                ct);
        }

        public Task RequestSyncStateAsync(CancellationToken ct = default)
        {
            EnsureHub();
            EnsureMatch();
            return _hub!.SyncStateAsync(MatchId!.Value, SessionToken!, ct);
        }

        public async Task LogoutAsync(CancellationToken ct = default)
        {
            await DisposeHubAsync();
            if (!string.IsNullOrEmpty(AccessToken))
                await _api.LogoutAsync(ct);

            PlayerId = null;
            AccessToken = null;
            RefreshToken = null;
            MatchId = null;
            SessionToken = null;
            CurrentState = null;
            Profile = null;
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeHubAsync();
            _api.Dispose();
        }

        private void HandleSyncState(GameStateDto state)
        {
            CurrentState = state;
            StateChanged?.Invoke(state);
        }

        private void ApplyTokens(Guid playerId, string accessToken, string refreshToken)
        {
            PlayerId = playerId;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            _api.SetAccessToken(accessToken);
        }

        private static CurrentPlayerProfileResponse MapGuestProfile(RegisterGuestResponse result) =>
            new(
                result.PlayerId,
                AccountType.Guest,
                result.Profile.Name,
                result.Profile.ImageId,
                result.Profile.Level,
                result.Profile.Coins,
                Array.Empty<PlayerInventoryItemDto>(),
                new PlayerStatisticsDto(result.Profile.Wins, result.Profile.Losses, result.Profile.Wins + result.Profile.Losses),
                DateTime.UtcNow);

        private async Task DisposeHubAsync()
        {
            if (_hub is null)
                return;

            _hub.OnSyncState -= HandleSyncState;
            await _hub.DisposeAsync();
            _hub = null;
        }

        private void EnsureAuthenticated()
        {
            if (string.IsNullOrEmpty(AccessToken))
                throw new InvalidOperationException("Not authenticated. Call RegisterGuestAsync first.");
        }

        private void ApplyQueueResult(JoinQueueResponse result)
        {
            if (!result.Queued && result.MatchId is Guid matchId && result.SessionToken is not null)
            {
                MatchId = matchId;
                SessionToken = result.SessionToken;
                StatusMessage?.Invoke("Match found.");
            }
            else
            {
                StatusMessage?.Invoke(result.Message);
            }
        }

        private void EnsureHub()
        {
            if (_hub is null || _hub.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Hub is not connected. Call ConnectHubAsync first.");
        }

        private void EnsureMatch()
        {
            if (MatchId is null || string.IsNullOrEmpty(SessionToken))
                throw new InvalidOperationException("No active match. Call JoinQueueAsync first.");
        }

        public static string NewIdempotencyKey() => Guid.NewGuid().ToString("N");
    }
}
