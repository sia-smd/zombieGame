namespace ZombieGame.Application.Tests.Game;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

public class StartGameHubIntegrationTests
{
    private const string BaseUrl = "http://localhost:5232";

    [Fact(Skip = "Requires API running at http://localhost:5232")]
    public async Task StartGame_Succeeds_ForMatchWithBot()
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };

        var deviceId = $"test-{Guid.NewGuid():N}";
        var guest = await http.PostAsJsonAsync("/api/account/register-guest", new
        {
            deviceId,
            platform = 0,
            appVersion = "1.0"
        });
        guest.EnsureSuccessStatusCode();
        var guestJson = await guest.Content.ReadFromJsonAsync<JsonElement>();
        var token = guestJson.GetProperty("accessToken").GetString()!;

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var q1 = await http.PostAsync("/api/matchmaking/queue/join", null);
        q1.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(12));

        var q2 = await http.PostAsync("/api/matchmaking/queue/join", null);
        q2.EnsureSuccessStatusCode();
        var queue = await q2.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = queue.GetProperty("matchId").GetGuid();
        var sessionToken = queue.GetProperty("sessionToken").GetString()!;

        var connection = new HubConnectionBuilder()
            .WithUrl($"{BaseUrl}/hubs/game", options => options.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .Build();

        await connection.StartAsync();

        await connection.InvokeAsync("JoinMatch", matchId, sessionToken);

        await connection.InvokeAsync("StartGame", matchId, sessionToken);

        var secondEx = await Record.ExceptionAsync(() =>
            connection.InvokeAsync("StartGame", matchId, sessionToken));

        Assert.Null(secondEx);

        await connection.StopAsync();
    }
}
