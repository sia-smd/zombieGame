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
        var match = await _matchService.GetMatchAsync(matchId, cancellationToken);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpGet("matches/{matchId:guid}/players")]
    public async Task<ActionResult<IReadOnlyList<MatchPlayerResponse>>> GetMatchPlayers(Guid matchId, CancellationToken cancellationToken)
    {
        return Ok(await _matchService.GetMatchPlayersAsync(matchId, cancellationToken));
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
