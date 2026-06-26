namespace ZombieGame.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZombieGame.Api.Extensions;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Profile;
using ZombieGame.Application.Interfaces;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IPlayerProfileService _profileService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IPlayerProfileService profileService, ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentPlayerProfileResponse>> GetCurrentPlayer(CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _profileService.GetCurrentPlayerAsync(playerId.Value, cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Get profile failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("update")]
    public async Task<ActionResult<CurrentPlayerProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _profileService.UpdateProfileAsync(playerId.Value, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Profile update failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
