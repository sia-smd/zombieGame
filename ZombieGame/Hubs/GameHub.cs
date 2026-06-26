namespace ZombieGame.Api.Hubs;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Game;
using ZombieGame.Application.Interfaces;

[Authorize]
public class GameHub : Hub
{
    private readonly IGameService _gameService;

    public GameHub(IGameService gameService) => _gameService = gameService;

    public async Task JoinMatch(Guid matchId, string sessionToken)
    {
        var userId = GetRequiredUserId();
        var result = await _gameService.JoinMatchAsync(userId, matchId, sessionToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, MatchGroup(matchId));
        await Clients.Caller.SendAsync("SyncState", result.State);
    }

    public async Task StartGame(Guid matchId, string sessionToken)
    {
        var userId = GetRequiredUserId();
        var result = await _gameService.StartGameAsync(userId, matchId, sessionToken);
        await BroadcastState(matchId, result);
    }

    public async Task PlayCard(Guid matchId, string sessionToken, PlayCardRequest request)
    {
        var userId = GetRequiredUserId();
        var result = await _gameService.PlayCardAsync(userId, matchId, sessionToken, request);
        await BroadcastState(matchId, result);
    }

    public async Task PassAction(Guid matchId, string sessionToken, PassActionRequest request)
    {
        var userId = GetRequiredUserId();
        var result = await _gameService.PassActionAsync(userId, matchId, sessionToken, request);
        await BroadcastState(matchId, result);
    }

    public async Task EndTurn(Guid matchId, string sessionToken, EndTurnRequest request)
    {
        var userId = GetRequiredUserId();
        var result = await _gameService.EndTurnAsync(userId, matchId, sessionToken, request);
        await BroadcastState(matchId, result);
    }

    public async Task VotePlayer(Guid matchId, string sessionToken, VotePlayerRequest request)
    {
        var userId = GetRequiredUserId();
        var result = await _gameService.VotePlayerAsync(userId, matchId, sessionToken, request);
        await BroadcastState(matchId, result);
    }

    public async Task SyncState(Guid matchId, string sessionToken)
    {
        var userId = GetRequiredUserId();
        var state = await _gameService.GetStateAsync(userId, matchId, sessionToken);
        await Clients.Caller.SendAsync("SyncState", state);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastState(Guid matchId, GameActionResult result)
    {
        if (result.State is not null)
            await Clients.Group(MatchGroup(matchId)).SendAsync("SyncState", result.State);

        await Clients.Group(MatchGroup(matchId)).SendAsync("GameEvent", new
        {
            success = result.Success,
            message = result.Message
        });
    }

    private static string MatchGroup(Guid matchId) => $"match-{matchId}";

    private Guid GetRequiredUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
            throw new HubException("Unauthorized");
        return userId;
    }
}
