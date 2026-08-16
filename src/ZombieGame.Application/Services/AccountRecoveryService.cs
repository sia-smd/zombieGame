namespace ZombieGame.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Account;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Security;
using ZombieGame.Application.Validation;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public sealed class AccountRecoveryService : IAccountRecoveryService
{
    public const string RecoverFailedMessage = "Unable to recover this account.";
    public const string GuestRequiredMessage = "Only a guest session can recover an existing account.";

    private readonly IUserRepository _userRepository;
    private readonly IPlayerProfileRepository _profileRepository;
    private readonly IPlayerSessionRepository _sessionRepository;
    private readonly IPlayerDeviceRepository _deviceRepository;
    private readonly IPlayerLoginLogRepository _loginLogRepository;
    private readonly IAccountRecoveryChallengeRepository _challengeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ISmsService _smsService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AccountSettings _accountSettings;
    private readonly ILogger<AccountRecoveryService> _logger;
    private readonly Random _random = new();

    public AccountRecoveryService(
        IUserRepository userRepository,
        IPlayerProfileRepository profileRepository,
        IPlayerSessionRepository sessionRepository,
        IPlayerDeviceRepository deviceRepository,
        IPlayerLoginLogRepository loginLogRepository,
        IAccountRecoveryChallengeRepository challengeRepository,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ISmsService smsService,
        IPasswordHasher passwordHasher,
        IOptions<AccountSettings> accountSettings,
        ILogger<AccountRecoveryService> logger)
    {
        _userRepository = userRepository;
        _profileRepository = profileRepository;
        _sessionRepository = sessionRepository;
        _deviceRepository = deviceRepository;
        _loginLogRepository = loginLogRepository;
        _challengeRepository = challengeRepository;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _smsService = smsService;
        _passwordHasher = passwordHasher;
        _accountSettings = accountSettings.Value;
        _logger = logger;
    }

    public async Task<bool> CanRecoverByMobileAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
            return false;

        var user = await FindByMobileAsync(mobileNumber, cancellationToken);
        return user is { AccountType: AccountType.Mobile, MobileVerified: true, PhoneNumber: not null };
    }

    private async Task<User?> FindByMobileAsync(string mobileNumber, CancellationToken cancellationToken)
    {
        foreach (var key in MobileNumberValidator.LookupKeys(mobileNumber))
        {
            var found = await _userRepository.GetByPhoneNumberAsync(key, cancellationToken);
            if (found is not null)
                return found;
        }

        return null;
    }

    public Task<bool> CanLinkGoogleAccountAsync(string googleSubjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> CanLinkAppleAccountAsync(string appleSubjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public async Task<RecoverAccountSendResponse> SendRecoverAccountOtpAsync(
        Guid guestPlayerId,
        RecoverAccountSendRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var guest = await RequireGuestAsync(guestPlayerId, cancellationToken);
        var target = await ResolveRecoverableTargetAsync(request.Username, request.Password, cancellationToken);

        if (target.Id == guest.Id)
            throw new ServiceException(RecoverFailedMessage);

        var code = GenerateVerificationCode();
        await _challengeRepository.InvalidateActiveAsync(
            guest.Id,
            target.Id,
            AccountRecoveryPurpose.RecoverAccount,
            cancellationToken);

        await _challengeRepository.AddAsync(new AccountRecoveryChallenge
        {
            Id = Guid.NewGuid(),
            GuestPlayerId = guest.Id,
            TargetPlayerId = target.Id,
            Purpose = AccountRecoveryPurpose.RecoverAccount,
            CodeHash = _passwordHasher.Hash(code),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_accountSettings.MobileVerificationExpirationMinutes)
        }, cancellationToken);

        await LogAsync(guest.Id, null, ipAddress, LoginResult.Success, "recover_account_otp_sent", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _smsService.SendVerificationCodeAsync(target.PhoneNumber!, code, cancellationToken);
        _logger.LogInformation(
            "Account recovery OTP sent for guest {GuestPlayerId} targeting {TargetPlayerId}",
            guest.Id,
            target.Id);

        return new RecoverAccountSendResponse(true, "Verification code sent.");
    }

    public async Task<RecoverAccountVerifyResponse> VerifyRecoverAccountOtpAsync(
        Guid guestPlayerId,
        Guid? guestSessionId,
        RecoverAccountVerifyRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.DeviceId)
            || string.IsNullOrWhiteSpace(request.AppVersion))
            throw new ServiceException("Verification code, device id, and app version are required.");

        var guest = await RequireGuestAsync(guestPlayerId, cancellationToken);
        var target = await ResolveRecoverableTargetAsync(request.Username, request.Password, cancellationToken);

        if (target.Id == guest.Id)
            throw new ServiceException(RecoverFailedMessage);

        var challenge = await _challengeRepository.GetActiveAsync(
            guest.Id,
            target.Id,
            AccountRecoveryPurpose.RecoverAccount,
            cancellationToken);

        if (challenge is null)
            throw new ServiceException("Invalid or expired verification code.");

        var submitted = request.Code.Trim();
        if (submitted != VerificationCodeRules.MasterOtp && challenge.ExpiresAt < DateTime.UtcNow)
            throw new ServiceException("Invalid or expired verification code.");

        if (!VerificationCodeRules.Matches(submitted, challenge.CodeHash, _passwordHasher))
            throw new ServiceException("Invalid or expired verification code.");

        challenge.ConsumedAt = DateTime.UtcNow;
        _challengeRepository.Update(challenge);

        if (guestSessionId is Guid sessionId)
        {
            var guestSession = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
            if (guestSession is not null && guestSession.PlayerId == guest.Id && guestSession.IsActive)
            {
                guestSession.IsActive = false;
                _sessionRepository.Update(guestSession);
            }
        }

        var now = DateTime.UtcNow;
        var device = await GetOrCreateDeviceAsync(
            target.Id,
            request.DeviceId,
            request.Platform,
            request.AppVersion,
            now,
            cancellationToken);
        var tokens = await CreateSessionAsync(target, device, cancellationToken);

        target.LastLoginAt = now;
        _userRepository.Update(target);
        await LogAsync(target.Id, request.DeviceId, ipAddress, LoginResult.Success, "recover_account", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var hydrated = await _userRepository.GetByIdWithProfileAsync(target.Id, cancellationToken) ?? target;
        var profile = hydrated.Profile;

        _logger.LogInformation(
            "Guest {GuestPlayerId} recovered account {TargetPlayerId}",
            guest.Id,
            target.Id);

        return new RecoverAccountVerifyResponse(
            target.Id,
            target.Username,
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessExpiresAt,
            tokens.RefreshExpiresAt,
            new GuestProfileDto(
                profile?.Name ?? target.Username,
                profile?.ImageId ?? _accountSettings.DefaultAvatarId,
                profile?.Level ?? 1,
                target.Coins,
                target.Wins,
                target.Losses));
    }

    private async Task<User> RequireGuestAsync(Guid guestPlayerId, CancellationToken cancellationToken)
    {
        var guest = await _userRepository.GetByIdAsync(guestPlayerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        if (guest.AccountType != AccountType.Guest)
            throw new ServiceException(GuestRequiredMessage);

        return guest;
    }

    private async Task<User> ResolveRecoverableTargetAsync(
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new ServiceException(RecoverFailedMessage);

        var target = await _userRepository.GetByUsernameAsync(username.Trim(), cancellationToken);
        if (target is null
            || target.AccountType != AccountType.Mobile
            || !target.MobileVerified
            || string.IsNullOrWhiteSpace(target.PhoneNumber)
            || !_passwordHasher.Verify(password, target.PasswordHash))
            throw new ServiceException(RecoverFailedMessage);

        return target;
    }

    private async Task<(string AccessToken, string RefreshToken, DateTime AccessExpiresAt, DateTime RefreshExpiresAt)> CreateSessionAsync(
        User user,
        PlayerDevice device,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var refreshToken = _tokenService.GenerateRefreshToken();
        var tokenPair = _tokenService.CreateTokenPair(user.Id, user.Username, sessionId);
        var now = DateTime.UtcNow;
        var refreshExpiresAt = now.AddDays(_accountSettings.RefreshTokenExpirationDays);

        await _sessionRepository.AddAsync(new PlayerSession
        {
            Id = sessionId,
            PlayerId = user.Id,
            AccessTokenJti = tokenPair.Jti,
            RefreshTokenHash = _tokenService.HashToken(refreshToken),
            CreatedAt = now,
            ExpireDate = tokenPair.AccessExpiresAt,
            RefreshExpireDate = refreshExpiresAt,
            IsActive = true,
            DeviceId = device.Id
        }, cancellationToken);

        return (tokenPair.AccessToken, refreshToken, tokenPair.AccessExpiresAt, refreshExpiresAt);
    }

    private async Task<PlayerDevice> GetOrCreateDeviceAsync(
        Guid playerId,
        string deviceId,
        DevicePlatform platform,
        string appVersion,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await _deviceRepository.GetByPlayerAndDeviceIdAsync(playerId, deviceId, cancellationToken);
        if (existing is not null)
        {
            existing.Platform = platform;
            existing.AppVersion = appVersion;
            existing.LastLoginAt = now;
            existing.IsActive = true;
            _deviceRepository.Update(existing);
            return existing;
        }

        var device = new PlayerDevice
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            DeviceId = deviceId,
            Platform = platform,
            AppVersion = appVersion,
            CreatedAt = now,
            LastLoginAt = now,
            IsActive = true
        };

        await _deviceRepository.AddAsync(device, cancellationToken);
        return device;
    }

    private string GenerateVerificationCode()
    {
        var max = (int)Math.Pow(10, _accountSettings.MobileVerificationCodeLength);
        return _random.Next(max / 10, max).ToString();
    }

    private Task LogAsync(
        Guid playerId,
        string? deviceId,
        string? ipAddress,
        LoginResult result,
        string action,
        CancellationToken cancellationToken) =>
        _loginLogRepository.AddAsync(new PlayerLoginLog
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            DeviceId = deviceId,
            IpAddress = ipAddress,
            LoginAt = DateTime.UtcNow,
            Result = result,
            Action = action
        }, cancellationToken);
}
