namespace ZombieGame.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Account;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Validation;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public sealed class AccountService : IAccountService
{
    private readonly IUserRepository _userRepository;
    private readonly IPlayerProfileRepository _profileRepository;
    private readonly IPlayerSessionRepository _sessionRepository;
    private readonly IPlayerDeviceRepository _deviceRepository;
    private readonly IPlayerLoginLogRepository _loginLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ISmsService _smsService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly GameSettings _gameSettings;
    private readonly AccountSettings _accountSettings;
    private readonly ILogger<AccountService> _logger;
    private readonly Random _random = new();

    public AccountService(
        IUserRepository userRepository,
        IPlayerProfileRepository profileRepository,
        IPlayerSessionRepository sessionRepository,
        IPlayerDeviceRepository deviceRepository,
        IPlayerLoginLogRepository loginLogRepository,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ISmsService smsService,
        IPasswordHasher passwordHasher,
        IOptions<GameSettings> gameSettings,
        IOptions<AccountSettings> accountSettings,
        ILogger<AccountService> logger)
    {
        _userRepository = userRepository;
        _profileRepository = profileRepository;
        _sessionRepository = sessionRepository;
        _deviceRepository = deviceRepository;
        _loginLogRepository = loginLogRepository;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _smsService = smsService;
        _passwordHasher = passwordHasher;
        _gameSettings = gameSettings.Value;
        _accountSettings = accountSettings.Value;
        _logger = logger;
    }

    public async Task<RegisterGuestResponse> RegisterGuestAsync(
        RegisterGuestRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            throw new ServiceException("DeviceId is required.");

        var playerId = Guid.NewGuid();
        var guestName = await GenerateUniqueGuestNameAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = playerId,
            Username = guestName,
            AccountType = AccountType.Guest,
            PasswordHash = _passwordHasher.Hash(Guid.NewGuid().ToString("N")),
            Coins = _gameSettings.StartingCoins,
            CreatedAt = now,
            LastLoginAt = now
        };

        var profile = new PlayerProfile
        {
            PlayerId = playerId,
            Name = guestName,
            ImageId = _accountSettings.DefaultAvatarId,
            Level = 1
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _profileRepository.AddAsync(profile, cancellationToken);

        var device = await GetOrCreateDeviceAsync(playerId, request.DeviceId, request.Platform, request.AppVersion, now, cancellationToken);
        var tokens = await CreateSessionAsync(user, device, cancellationToken);

        await LogAsync(playerId, request.DeviceId, ipAddress, LoginResult.Success, "guest_registration", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Guest player {PlayerId} registered on device {DeviceId}", playerId, request.DeviceId);

        return new RegisterGuestResponse(
            playerId,
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessExpiresAt,
            tokens.RefreshExpiresAt,
            new GuestProfileDto(profile.Name, profile.ImageId, profile.Level, user.Coins, user.Wins, user.Losses));
    }

    public async Task<AddMobileResponse> AddMobileAsync(
        Guid playerId,
        AddMobileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!MobileNumberValidator.IsValid(request.MobileNumber))
            throw new ServiceException("Invalid mobile number format.");

        var mobile = MobileNumberValidator.Normalize(request.MobileNumber);
        var user = await _userRepository.GetByIdAsync(playerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        if (user.AccountType == AccountType.Mobile && user.MobileVerified && user.PhoneNumber == mobile)
            return new AddMobileResponse(true, false, "Mobile number is already linked.");

        if (await _userRepository.ExistsByPhoneNumberAsync(mobile, cancellationToken))
            throw new ServiceException("Mobile number is already registered to another account.");

        var code = GenerateVerificationCode();
        user.PendingPhoneNumber = mobile;
        user.MobileVerified = false;
        user.MobileVerificationCodeHash = _passwordHasher.Hash(code);
        user.MobileVerificationExpiresAt = DateTime.UtcNow.AddMinutes(_accountSettings.MobileVerificationExpirationMinutes);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _smsService.SendVerificationCodeAsync(mobile, code, cancellationToken);
        _logger.LogInformation("Mobile linking initiated for player {PlayerId}", playerId);

        return new AddMobileResponse(true, true, "Verification code sent.");
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new ServiceException("Refresh token is required.");

        var hash = _tokenService.HashToken(request.RefreshToken);
        var session = await _sessionRepository.GetByRefreshTokenHashAsync(hash, cancellationToken)
            ?? throw new ServiceException("Invalid refresh token.");

        if (!session.IsActive)
            throw new ServiceException("Session is no longer active.");

        if (session.RefreshExpireDate < DateTime.UtcNow)
        {
            session.IsActive = false;
            _sessionRepository.Update(session);
            await LogAsync(session.PlayerId, deviceId, ipAddress, LoginResult.Failure, "token_refresh_expired", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ServiceException("Refresh token has expired.");
        }

        var user = await _userRepository.GetByIdWithProfileAsync(session.PlayerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        var refreshToken = _tokenService.GenerateRefreshToken();
        var tokenPair = _tokenService.CreateTokenPair(user.Id, user.Username, session.Id);
        var now = DateTime.UtcNow;

        session.AccessTokenJti = tokenPair.Jti;
        session.RefreshTokenHash = _tokenService.HashToken(refreshToken);
        session.ExpireDate = tokenPair.AccessExpiresAt;
        session.RefreshExpireDate = now.AddDays(_accountSettings.RefreshTokenExpirationDays);
        session.IsActive = true;
        _sessionRepository.Update(session);

        user.LastLoginAt = now;
        _userRepository.Update(user);

        if (session.DeviceId is Guid devicePk)
        {
            var device = await _deviceRepository.GetByIdAsync(devicePk, cancellationToken);
            if (device is not null)
            {
                device.LastLoginAt = now;
                _deviceRepository.Update(device);
            }
        }

        await LogAsync(user.Id, deviceId, ipAddress, LoginResult.Success, "token_refresh", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Token refreshed for player {PlayerId}, session {SessionId}", user.Id, session.Id);

        return new RefreshTokenResponse(
            tokenPair.AccessToken,
            refreshToken,
            tokenPair.AccessExpiresAt,
            session.RefreshExpireDate);
    }

    public async Task<LogoutResponse> LogoutAsync(
        Guid playerId,
        Guid? sessionId,
        string? ipAddress,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId is null)
            throw new ServiceException("Session id is missing from token.");

        var session = await _sessionRepository.GetByIdAsync(sessionId.Value, cancellationToken);
        if (session is null || session.PlayerId != playerId)
            throw new ServiceException("Session not found.");

        session.IsActive = false;
        _sessionRepository.Update(session);
        await LogAsync(playerId, deviceId, ipAddress, LoginResult.Success, "logout", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Player {PlayerId} logged out session {SessionId}", playerId, sessionId);

        return new LogoutResponse(true, "Logged out successfully.");
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

        var session = new PlayerSession
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
        };

        await _sessionRepository.AddAsync(session, cancellationToken);

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

    private async Task<string> GenerateUniqueGuestNameAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var name = $"{_accountSettings.GuestNamePrefix}{_random.Next(100000, 999999)}";
            if (!await _userRepository.ExistsByUsernameAsync(name, cancellationToken))
                return name;
        }

        return $"{_accountSettings.GuestNamePrefix}{Guid.NewGuid().ToString()[..8]}";
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
