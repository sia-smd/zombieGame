using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ZombieGame.UnityClient.Networking.Models;

namespace ZombieGame.UnityClient.Networking
{
    /// <summary>REST client for account, profile, and matchmaking.</summary>
    public sealed class ZombieGameApiClient : IDisposable
    {
        private readonly HttpClient _http;

        public ZombieGameApiClient(string baseUrl)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        }

        public void SetAccessToken(string token) =>
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        public async Task<RegisterGuestResponse?> RegisterGuestAsync(RegisterGuestRequest request, CancellationToken ct = default)
        {
            var response = await HttpJson.PostJsonAsync(_http, "api/account/register-guest", request, ct);
            await ApiException.ThrowIfFailedAsync(response);
            var result = await HttpJson.ReadAsync<RegisterGuestResponse>(response.Content, ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task<RefreshTokenResponse?> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
        {
            var response = await HttpJson.PostJsonAsync(_http, "api/account/refresh-token", request, ct);
            await ApiException.ThrowIfFailedAsync(response);
            var result = await HttpJson.ReadAsync<RefreshTokenResponse>(response.Content, ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task LogoutAsync(CancellationToken ct = default)
        {
            var response = await _http.PostAsync("api/account/logout", null, ct);
            await ApiException.ThrowIfFailedAsync(response);
        }

        public async Task<AccountLoginResponse?> LoginAsync(AccountLoginRequest request, CancellationToken ct = default)
        {
            var response = await HttpJson.PostJsonAsync(_http, "api/account/login", request, ct);
            await ApiException.ThrowIfFailedAsync(response);
            var result = await HttpJson.ReadAsync<AccountLoginResponse>(response.Content, ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task<CurrentPlayerProfileResponse?> GetProfileMeAsync(CancellationToken ct = default)
        {
            var response = await _http.GetAsync("api/profile/me", ct);
            await ApiException.ThrowIfFailedAsync(response);
            return await HttpJson.ReadAsync<CurrentPlayerProfileResponse>(response.Content, ct);
        }

        public async Task<CurrentPlayerProfileResponse?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
        {
            var response = await HttpJson.PutJsonAsync(_http, "api/profile/update", request, ct);
            await ApiException.ThrowIfFailedAsync(response);
            return await HttpJson.ReadAsync<CurrentPlayerProfileResponse>(response.Content, ct);
        }

        public async Task<JoinQueueResponse?> JoinQueueAsync(CancellationToken ct = default)
        {
            var response = await _http.PostAsync("api/matchmaking/queue/join", null, ct);
            await ApiException.ThrowIfFailedAsync(response);
            return await HttpJson.ReadAsync<JoinQueueResponse>(response.Content, ct);
        }

        public async Task<QueueStatusResponse?> GetQueueStatusAsync(CancellationToken ct = default)
        {
            var response = await _http.GetAsync("api/matchmaking/queue/status", ct);
            await ApiException.ThrowIfFailedAsync(response);
            return await HttpJson.ReadAsync<QueueStatusResponse>(response.Content, ct);
        }

        public async Task LeaveQueueAsync(CancellationToken ct = default)
        {
            var response = await _http.PostAsync("api/matchmaking/queue/leave", null, ct);
            await ApiException.ThrowIfFailedAsync(response);
        }

        public void Dispose() => _http.Dispose();
    }

    public record JoinQueueResponse(bool Queued, string Message, Guid? MatchId = null, string? SessionToken = null);
    public record QueueStatusResponse(bool InQueue, int QueueCount);
}
