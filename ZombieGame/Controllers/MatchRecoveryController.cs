namespace ZombieGame.Api.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZombieGame.Application.Interfaces;

[ApiController]
[Route("api/match")]
[Authorize]
public class MatchRecoveryController : ControllerBase
{
    private readonly IActiveMatchQueryService _activeMatch;

    public MatchRecoveryController(IActiveMatchQueryService activeMatch) => _activeMatch = activeMatch;

    [HttpGet("active")]
    public async Task<ActionResult<ActiveMatchApiResponse>> GetActive(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        var active = await _activeMatch.GetActiveMatchForPlayerAsync(userId, cancellationToken);

        return Ok(new ActiveMatchApiResponse(
            active.HasActiveMatch,
            active.MatchId,
            active.Day,
            active.Phase.ToString(),
            active.BattleId,
            active.Alive,
            active.Role.ToString(),
            active.RoomName,
            active.PlayersAlive,
            active.SessionToken));
    }

    private Guid GetRequiredUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException();
        return userId;
    }
}

public sealed record ActiveMatchApiResponse(
    bool HasActiveMatch,
    Guid? MatchId,
    int Day,
    string Phase,
    Guid? BattleId,
    bool Alive,
    string Role,
    string? RoomName,
    int PlayersAlive,
    string? SessionToken);
