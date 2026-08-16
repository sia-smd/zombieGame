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
    private readonly IPlayerProgressService _progressService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IPlayerProfileService profileService,
        IPlayerProgressService progressService,
        ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _progressService = progressService;
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

    [HttpGet("achievements")]
    public async Task<ActionResult<PlayerProgressResponse>> GetAchievements(CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        return Ok(await _progressService.GetProgressAsync(playerId.Value, cancellationToken));
    }

    [HttpGet("avatars")]
    public ActionResult<IReadOnlyList<AvatarOptionDto>> GetAvatars()
    {
        return Ok(_profileService.GetAvatarOptions());
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

    [HttpPut("username")]
    public async Task<ActionResult<CurrentPlayerProfileResponse>> UpdateUsername(
        [FromBody] UpdateUsernameRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _profileService.UpdateUsernameAsync(playerId.Value, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Username update failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("avatar")]
    public async Task<ActionResult<CurrentPlayerProfileResponse>> UploadAvatar(
        [FromBody] UploadAvatarRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _profileService.UploadAvatarAsync(playerId.Value, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Avatar upload failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
