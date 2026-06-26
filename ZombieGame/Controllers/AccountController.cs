namespace ZombieGame.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZombieGame.Api.Extensions;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Account;
using ZombieGame.Application.Interfaces;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAccountService accountService, ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    [HttpPost("register-guest")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterGuestResponse>> RegisterGuest(
        [FromBody] RegisterGuestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _accountService.RegisterGuestAsync(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);
            return Ok(response);
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Guest registration failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("add-mobile")]
    [Authorize]
    public async Task<ActionResult<AddMobileResponse>> AddMobile(
        [FromBody] AddMobileRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _accountService.AddMobileAsync(playerId.Value, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Add mobile failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<RefreshTokenResponse>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
            return Ok(await _accountService.RefreshTokenAsync(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                deviceId,
                cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<LogoutResponse>> Logout(CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
            return Ok(await _accountService.LogoutAsync(
                playerId.Value,
                User.GetSessionId(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                deviceId,
                cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Logout failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
