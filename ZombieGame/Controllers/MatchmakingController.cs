namespace ZombieGame.Api.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Matchmaking;
using ZombieGame.Application.Interfaces;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatchmakingController : ControllerBase
{
    private readonly IMatchmakingService _matchmakingService;
    private readonly IMatchService _matchService;

    public MatchmakingController(IMatchmakingService matchmakingService, IMatchService matchService)
    {
        _matchmakingService = matchmakingService;
        _matchService = matchService;
    }

    [HttpGet("room-config")]
    public ActionResult<RoomConfigResponse> GetRoomConfig() =>
        Ok(_matchmakingService.GetRoomConfig());

    [HttpPost("rooms")]
    public async Task<ActionResult<CreateRoomResponse>> CreateRoom(
        [FromBody] CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        try
        {
            return Ok(await _matchmakingService.CreateRoomAsync(userId, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("rooms")]
    public async Task<ActionResult<IReadOnlyList<OpenRoomDto>>> ListOpenRooms(CancellationToken cancellationToken)
    {
        return Ok(await _matchmakingService.ListOpenRoomsAsync(cancellationToken));
    }

    [HttpPost("join-room")]
    public async Task<ActionResult<JoinOpenRoomResponse>> JoinOpenRoom(
        [FromBody] JoinOpenRoomRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        try
        {
            return Ok(await _matchmakingService.JoinOpenRoomAsync(userId, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("leave-room")]
    public async Task<IActionResult> LeaveWaitingRoom(
        [FromBody] LeaveWaitingRoomRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        try
        {
            await _matchmakingService.LeaveWaitingRoomAsync(userId, request.MatchId, cancellationToken);
            return NoContent();
        }
        catch (ServiceException ex)
        {
            return BadRequest(new { message = ex.Message, code = ex.Code });
        }
    }

    [HttpPost("invites")]
    public async Task<ActionResult<SendRoomInviteResponse>> SendInvite(
        [FromBody] SendRoomInviteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        try
        {
            return Ok(await _matchmakingService.SendWaitingRoomInviteAsync(userId, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            return BadRequest(new { message = ex.Message, code = ex.Code });
        }
    }

    [HttpPost("invites/{inviteId:guid}/accept")]
    public async Task<ActionResult<JoinOpenRoomResponse>> AcceptInvite(
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        try
        {
            return Ok(await _matchmakingService.AcceptWaitingRoomInviteAsync(userId, inviteId, cancellationToken));
        }
        catch (ServiceException ex)
        {
            return BadRequest(new { message = ex.Message, code = ex.Code });
        }
    }

    [HttpPost("invites/{inviteId:guid}/deny")]
    public async Task<IActionResult> DenyInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        try
        {
            await _matchmakingService.DenyWaitingRoomInviteAsync(userId, inviteId, cancellationToken);
            return NoContent();
        }
        catch (ServiceException ex)
        {
            return BadRequest(new { message = ex.Message, code = ex.Code });
        }
    }

    [HttpPost("queue/join")]
    public async Task<ActionResult<JoinQueueResponse>> JoinQueue(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();

        try
        {
            return Ok(await _matchmakingService.JoinQueueAsync(userId, cancellationToken));
        }
        catch (ServiceException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("queue/leave")]
    public async Task<IActionResult> LeaveQueue()
    {
        await _matchmakingService.LeaveQueueAsync(GetRequiredUserId());
        return NoContent();
    }

    [HttpGet("queue/status")]
    public ActionResult<object> QueueStatus()
    {
        var userId = GetRequiredUserId();
        return Ok(new
        {
            inQueue = _matchmakingService.IsInQueue(userId),
            queueCount = _matchmakingService.QueueCount
        });
    }

    [HttpGet("matches/{matchId:guid}")]
    public async Task<ActionResult<MatchSummaryResponse>> GetMatch(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _matchService.GetMatchAsync(GetRequiredUserId(), matchId, cancellationToken);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpGet("matches/{matchId:guid}/players")]
    public async Task<ActionResult<IReadOnlyList<MatchPlayerResponse>>> GetMatchPlayers(Guid matchId, CancellationToken cancellationToken)
    {
        var players = await _matchService.GetMatchPlayersAsync(
            GetRequiredUserId(),
            matchId,
            cancellationToken);
        return players is null ? NotFound() : Ok(players);
    }

    private Guid GetRequiredUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException();
        return userId;
    }
}
