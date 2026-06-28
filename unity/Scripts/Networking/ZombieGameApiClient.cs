using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
            var response = await _http.PostAsJsonAsync("api/account/register-guest", request, NetworkingJson.Options, ct);
            await ApiException.ThrowIfFailedAsync(response);
            var result = await response.Content.ReadFromJsonAsync<RegisterGuestResponse>(NetworkingJson.Options, ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task<RefreshTokenResponse?> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
        {
            var response = await _http.PostAsJsonAsync("api/account/refresh-token", request, NetworkingJson.Options, ct);
            await ApiException.ThrowIfFailedAsync(response);
            var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(NetworkingJson.Options, ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task LogoutAsync(CancellationToken ct = default)
        {
            var response = await _http.PostAsync("api/account/logout", null, ct);
            await ApiException.ThrowIfFailedAsync(response);
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var response = await _http.PostAsJsonAsync("api/auth/register", request, NetworkingJson.Options, ct);
            await ApiException.ThrowIfFailedAsync(response);
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>(NetworkingJson.Options, ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", request, NetworkingJson.Options, ct);
            await ApiException.ThrowIfFailedAsync(response);
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>(NetworkingJson.Options, ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task<CurrentPlayerProfileResponse?> GetProfileMeAsync(CancellationToken ct = default)
        {
            var response = await _http.GetAsync("api/profile/me", ct);
            await ApiException.ThrowIfFailedAsync(response);
            return await response.Content.ReadFromJsonAsync<CurrentPlayerProfileResponse>(NetworkingJson.Options, ct);
        }

        public async Task<CurrentPlayerProfileResponse?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
        {
            var response = await _http.PutAsJsonAsync("api/profile/update", request, NetworkingJson.Options, ct);
            await ApiException.ThrowIfFailedAsync(response);
            return await response.Content.ReadFromJsonAsync<CurrentPlayerProfileResponse>(NetworkingJson.Options, ct);
        }

        public async Task<JoinQueueResponse?> JoinQueueAsync(CancellationToken ct = default)
        {
            var response = await _http.PostAsync("api/matchmaking/queue/join", null, ct);
            await ApiException.ThrowIfFailedAsync(response);
            return await response.Content.ReadFromJsonAsync<JoinQueueResponse>(NetworkingJson.Options, ct);
        }

        public async Task<QueueStatusResponse?> GetQueueStatusAsync(CancellationToken ct = default)
        {
            var response = await _http.GetAsync("api/matchmaking/queue/status", ct);
            await ApiException.ThrowIfFailedAsync(response);
            return await response.Content.ReadFromJsonAsync<QueueStatusResponse>(NetworkingJson.Options, ct);
        }

        public async Task LeaveQueueAsync(CancellationToken ct = default)
        {
            var response = await _http.PostAsync("api/matchmaking/queue/leave", null, ct);
            await ApiException.ThrowIfFailedAsync(response);
        }

        public void Dispose() => _http.Dispose();
    }

    public record RegisterRequest(string Username, string PhoneNumber, string Password, string? Email = null);
    public record LoginRequest(string PhoneNumber, string Password);
    public record AuthResponse(Guid UserId, string Username, string AccessToken);
    public record JoinQueueResponse(bool Queued, string Message, Guid? MatchId = null, string? SessionToken = null);
    public record QueueStatusResponse(bool InQueue, int QueueCount);
}
