namespace ZombieGame.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZombieGame.Api.Extensions;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Account;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IAccountRecoveryService _recoveryService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAccountService accountService,
        IAccountRecoveryService recoveryService,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _recoveryService = recoveryService;
        _logger = logger;
    }

    [HttpPost("register-guest")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.AuthPolicy)]
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

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.AuthPolicy)]
    public async Task<ActionResult<AccountLoginResponse>> Login(
        [FromBody] AccountLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _accountService.LoginAsync(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Account login failed: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("add-mobile")]
    [Authorize]
    [EnableRateLimiting(RateLimitSettings.SmsPolicy)]
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

    [HttpPost("verify-mobile")]
    [Authorize]
    [EnableRateLimiting(RateLimitSettings.SmsPolicy)]
    public async Task<ActionResult<VerifyMobileResponse>> VerifyMobile(
        [FromBody] VerifyMobileRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _accountService.VerifyMobileAsync(playerId.Value, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Verify mobile failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _accountService.ChangePasswordAsync(playerId.Value, request, cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Change password failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.AuthPolicy)]
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

    [HttpPost("recover/send-otp")]
    [Authorize]
    [EnableRateLimiting(RateLimitSettings.SmsPolicy)]
    public async Task<ActionResult<RecoverAccountSendResponse>> SendRecoverAccountOtp(
        [FromBody] RecoverAccountSendRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _recoveryService.SendRecoverAccountOtpAsync(
                playerId.Value,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Recover-account OTP send failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("recover/verify-otp")]
    [Authorize]
    [EnableRateLimiting(RateLimitSettings.AuthPolicy)]
    public async Task<ActionResult<RecoverAccountVerifyResponse>> VerifyRecoverAccountOtp(
        [FromBody] RecoverAccountVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.GetUserId();
        if (playerId is null) return Unauthorized();

        try
        {
            return Ok(await _recoveryService.VerifyRecoverAccountOtpAsync(
                playerId.Value,
                User.GetSessionId(),
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning("Recover-account OTP verify failed for player {PlayerId}: {Message}", playerId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
