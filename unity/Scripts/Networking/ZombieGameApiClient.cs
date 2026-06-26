using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ZombieGame.UnityClient.Networking
{
    /// <summary>
    /// REST client stub for auth and matchmaking.
    /// Wire this into a Unity MonoBehaviour or use as a plain C# service.
    /// </summary>
    public sealed class ZombieGameApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private string? _accessToken;

        public ZombieGameApiClient(string baseUrl)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        }

        public void SetAccessToken(string token)
        {
            _accessToken = token;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var response = await _http.PostAsJsonAsync("api/auth/register", request, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", request, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
            if (result?.AccessToken is not null)
                SetAccessToken(result.AccessToken);
            return result;
        }

        public async Task<UserProfileResponse?> GetProfileAsync(CancellationToken ct = default)
        {
            var response = await _http.GetAsync("api/auth/profile", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserProfileResponse>(cancellationToken: ct);
        }

        public async Task<JoinQueueResponse?> JoinQueueAsync(CancellationToken ct = default)
        {
            var response = await _http.PostAsync("api/matchmaking/queue/join", null, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JoinQueueResponse>(cancellationToken: ct);
        }

        public void Dispose() => _http.Dispose();
    }

    public record RegisterRequest(string Username, string PhoneNumber, string Password, string? Email = null);
    public record LoginRequest(string PhoneNumber, string Password);
    public record AuthResponse(Guid UserId, string Username, string AccessToken);
    public record UserProfileResponse(Guid Id, string Username, string? Email, string PhoneNumber, int Coins, int Wins, int Losses, DateTime CreatedAt);
    public record JoinQueueResponse(bool Queued, string Message, Guid? MatchId = null, string? SessionToken = null);
}
