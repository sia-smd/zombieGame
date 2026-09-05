namespace ZombieGame.Api.Hubs;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ZombieGame.Application.DTOs.Room;
using ZombieGame.Application.Interfaces;

[Authorize]
public class RoomHub : Hub
{
    private readonly IRoomService _roomService;
    private readonly IMatchmakingService _matchmakingService;
    private readonly IUserPresenceTracker _presence;

    public RoomHub(
        IRoomService roomService,
        IMatchmakingService matchmakingService,
        IUserPresenceTracker presence)
    {
        _roomService = roomService;
        _matchmakingService = matchmakingService;
        _presence = presence;
    }

    public async Task JoinRoom(Guid matchId, string sessionToken)
    {
        var userId = GetRequiredUserId();
        var result = await _roomService.JoinRoomAsync(userId, matchId, sessionToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(matchId));
        await Clients.Group(RoomGroup(matchId)).SendAsync("RoomUpdated", result.State);
    }

    public async Task LeaveWaitingRoom(Guid matchId)
    {
        var userId = GetRequiredUserId();
        await _matchmakingService.LeaveWaitingRoomAsync(userId, matchId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(matchId));
    }

    public async Task ResumeMatch(Guid matchId, string sessionToken)
    {
        var userId = GetRequiredUserId();
        var resume = await _roomService.ResumeMatchAsync(userId, matchId, sessionToken);
        if (resume is null)
            throw new HubException("Unable to resume match.");

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(matchId));
        if (resume.CurrentBattle is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, BattleGroup(resume.CurrentBattle.BattleId));

        var state = await _roomService.GetPublicStateAsync(userId, matchId, sessionToken);
        await Clients.Caller.SendAsync("RoomUpdated", state);
        await Clients.Caller.SendAsync("MatchResumed", resume.Presence);
    }

    public async Task StartGame(Guid matchId, string sessionToken)
    {
        var userId = GetRequiredUserId();
        var result = await _roomService.StartGameAsync(userId, matchId, sessionToken);
        await Clients.Group(RoomGroup(matchId)).SendAsync("GameStarted", result.State);
        await Clients.Group(RoomGroup(matchId)).SendAsync("RoomUpdated", result.State);
    }

    public async Task SendInvitation(Guid matchId, string sessionToken, SendInvitationRequest request)
    {
        var userId = GetRequiredUserId();
        var result = await _roomService.SendInvitationAsync(userId, matchId, sessionToken, request.TargetUserId);
        await Clients.Group(RoomGroup(matchId)).SendAsync("RoomUpdated", result.State);
    }

    public async Task RespondInvitation(Guid matchId, string sessionToken, RespondInvitationRequest request)
    {
        var userId = GetRequiredUserId();
        var result = await _roomService.RespondInvitationAsync(userId, matchId, sessionToken, request.InvitationId, request.Accept);
        await Clients.Group(RoomGroup(matchId)).SendAsync("RoomUpdated", result.State);
    }

    public async Task MarkPhaseReady(Guid matchId, string sessionToken)
    {
        var userId = GetRequiredUserId();
        var result = await _roomService.MarkPhaseReadyAsync(userId, matchId, sessionToken);
        await Clients.Group(RoomGroup(matchId)).SendAsync("RoomUpdated", result.State);
    }

    public async Task PlayCardInBattle(Guid matchId, string sessionToken, BattlePlayCardRequest request)
    {
        var userId = GetRequiredUserId();
        // State machine already broadcasts public RoomUpdated and pushes private snapshots
        // to both humans in the pair (infection/role/hand). Do not rebroadcast the actor's
        // private result.State to the room/battle groups — that leaked hands and overwrote
        // the opponent's myBattle with the actor's me.
        await _roomService.PlayCardInBattleAsync(
            userId, matchId, sessionToken, request.PairId, request.CardId, request.TargetUserId, request.InventorySlotIndex);

        var privateState = await _roomService.GetPublicStateAsync(userId, matchId, sessionToken);
        await Clients.Caller.SendAsync("RoomUpdated", privateState);
    }

    public async Task FinishBattleTurn(Guid matchId, string sessionToken, BattleFinishTurnRequest request)
    {
        var userId = GetRequiredUserId();
        await _roomService.FinishBattleTurnAsync(userId, matchId, sessionToken, request.PairId);

        var privateState = await _roomService.GetPublicStateAsync(userId, matchId, sessionToken);
        await Clients.Caller.SendAsync("RoomUpdated", privateState);
    }

    public async Task PassInBattle(Guid matchId, string sessionToken, BattlePassRequest request)
    {
        var userId = GetRequiredUserId();
        await _roomService.FinishBattleTurnAsync(userId, matchId, sessionToken, request.PairId);

        var privateState = await _roomService.GetPublicStateAsync(userId, matchId, sessionToken);
        await Clients.Caller.SendAsync("RoomUpdated", privateState);
    }

    public async Task SendChat(Guid matchId, string sessionToken, SendChatRequest request)
    {
        var userId = GetRequiredUserId();
        await _roomService.SendChatAsync(userId, matchId, sessionToken, request.Text);
    }

    public async Task Vote(Guid matchId, string sessionToken, CastRoomVoteRequest request)
    {
        var userId = GetRequiredUserId();
        var result = await _roomService.VoteAsync(userId, matchId, sessionToken, request.TargetUserId);
        await Clients.Group(RoomGroup(matchId)).SendAsync("RoomUpdated", result.State);
    }

    public async Task SyncRoom(Guid matchId, string sessionToken)
    {
        var userId = GetRequiredUserId();
        var state = await _roomService.GetPublicStateAsync(userId, matchId, sessionToken);
        await Clients.Caller.SendAsync("RoomUpdated", state);
    }

    public async Task JoinBattle(Guid matchId, string sessionToken, Guid battleId)
    {
        var userId = GetRequiredUserId();
        var result = await _roomService.JoinBattleAsync(userId, matchId, sessionToken, battleId);
        if (!result.Allowed)
            throw new HubException(result.Message ?? "Not allowed to join this battle.");

        await Groups.AddToGroupAsync(Context.ConnectionId, BattleGroup(battleId));
        // Private hand/actions for this viewer — group broadcasts never include them.
        var privateState = await _roomService.GetPublicStateAsync(userId, matchId, sessionToken);
        await Clients.Caller.SendAsync("RoomUpdated", privateState);
    }

    public override Task OnConnectedAsync()
    {
        var userId = TryGetUserId();
        if (userId is Guid id)
            _presence.AddConnection(id, Context.ConnectionId);

        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = TryGetUserId();
        if (userId is Guid id)
        {
            _presence.RemoveConnection(id, Context.ConnectionId);
            var result = await _roomService.OnPlayerDisconnectedAsync(id);
            if (result?.State is not null)
                await Clients.Group(RoomGroup(result.State.MatchId)).SendAsync("RoomUpdated", result.State);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string RoomGroup(Guid matchId) => $"room-{matchId}";
    private static string BattleGroup(Guid battleId) => $"battle-{battleId}";

    private Guid GetRequiredUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
            throw new HubException("Unauthorized");
        return userId;
    }

    private Guid? TryGetUserId() =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub"), out var userId)
            ? userId
            : null;
}
