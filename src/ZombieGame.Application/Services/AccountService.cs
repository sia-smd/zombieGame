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
    private readonly IForbiddenWordsService _forbiddenWords;
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
        IForbiddenWordsService forbiddenWords,
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
        _forbiddenWords = forbiddenWords;
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
        var guestName = await ResolveGuestNameAsync(request.Nickname, cancellationToken);
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

    public async Task<AccountLoginResponse> LoginAsync(
        AccountLoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (!MobileNumberValidator.IsValid(request.PhoneNumber))
            throw new ServiceException("Invalid phone number or password.");
        if (string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.DeviceId)
            || string.IsNullOrWhiteSpace(request.AppVersion))
            throw new ServiceException("Phone number, password, device id, and app version are required.");

        var mobile = MobileNumberValidator.Normalize(request.PhoneNumber);
        var user = await FindByMobileAsync(mobile, cancellationToken);
        if (user is null
            || user.AccountType != AccountType.Mobile
            || !user.MobileVerified
            || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null)
            {
                await LogAsync(
                    user.Id,
                    request.DeviceId,
                    ipAddress,
                    LoginResult.Failure,
                    "login_invalid_credentials",
                    cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            throw new ServiceException("Invalid phone number or password.");
        }

        var now = DateTime.UtcNow;
        var device = await GetOrCreateDeviceAsync(
            user.Id,
            request.DeviceId,
            request.Platform,
            request.AppVersion,
            now,
            cancellationToken);
        var tokens = await CreateSessionAsync(user, device, cancellationToken);

        user.LastLoginAt = now;
        _userRepository.Update(user);
        await LogAsync(user.Id, request.DeviceId, ipAddress, LoginResult.Success, "login", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var hydratedUser = await _userRepository.GetByIdWithProfileAsync(user.Id, cancellationToken) ?? user;
        var profile = hydratedUser.Profile;

        _logger.LogInformation("Player {PlayerId} logged in on device {DeviceId}", user.Id, request.DeviceId);

        return new AccountLoginResponse(
            user.Id,
            user.Username,
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessExpiresAt,
            tokens.RefreshExpiresAt,
            new GuestProfileDto(
                profile?.Name ?? user.Username,
                profile?.ImageId ?? _accountSettings.DefaultAvatarId,
                profile?.Level ?? 1,
                user.Coins,
                user.Wins,
                user.Losses));
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

        if (user.AccountType == AccountType.Mobile && user.MobileVerified
            && user.PhoneNumber is not null
            && MobileNumberValidator.Normalize(user.PhoneNumber) == mobile)
            return new AddMobileResponse(true, false, "Mobile number is already linked.");

        if (await IsMobileTakenByOtherAsync(mobile, user.Id, cancellationToken))
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

    public async Task<VerifyMobileResponse> VerifyMobileAsync(
        Guid playerId,
        VerifyMobileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!MobileNumberValidator.IsValid(request.MobileNumber))
            throw new ServiceException("Invalid mobile number format.");

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ServiceException("Verification code is required.");

        var mobile = MobileNumberValidator.Normalize(request.MobileNumber);
        var user = await _userRepository.GetByIdAsync(playerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        if (user.PendingPhoneNumber is null
            || MobileNumberValidator.Normalize(user.PendingPhoneNumber) != mobile)
            throw new ServiceException("No pending mobile verification for this number.");

        var submitted = request.Code.Trim();
        if (submitted != VerificationCodeRules.MasterOtp)
        {
            if (user.MobileVerificationExpiresAt is null || user.MobileVerificationExpiresAt < DateTime.UtcNow)
                throw new ServiceException("Verification code has expired.");
        }

        if (!VerificationCodeRules.Matches(submitted, user.MobileVerificationCodeHash, _passwordHasher))
            throw new ServiceException("Invalid verification code.");

        user.PhoneNumber = mobile;
        user.PendingPhoneNumber = null;
        user.MobileVerified = true;
        user.MobileVerificationCodeHash = null;
        user.MobileVerificationExpiresAt = null;
        user.AccountType = AccountType.Mobile;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mobile verified for player {PlayerId}", playerId);

        return new VerifyMobileResponse(true, "Mobile number linked successfully.");
    }

    public async Task<ChangePasswordResponse> ChangePasswordAsync(
        Guid playerId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            throw new ServiceException("Password must be at least 6 characters.");

        var user = await _userRepository.GetByIdAsync(playerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        if (user.AccountType == AccountType.Mobile)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                throw new ServiceException("Current password is required.");

            if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
                throw new ServiceException("Current password is incorrect.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for player {PlayerId}", playerId);

        return new ChangePasswordResponse(true, "Password updated successfully.");
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

    private async Task<string> ResolveGuestNameAsync(string? nickname, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(nickname))
        {
            var trimmed = nickname.Trim();
            new ProfileNameValidator(_forbiddenWords, _accountSettings.MaxProfileNameLength).Validate(trimmed);

            if (await _userRepository.ExistsByUsernameAsync(trimmed, cancellationToken))
                throw new ServiceException("This nickname is already taken.");

            return trimmed;
        }

        return await GenerateUniqueGuestNameAsync(cancellationToken);
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

    private async Task<bool> IsMobileTakenByOtherAsync(string mobileNumber, Guid userId, CancellationToken cancellationToken)
    {
        var found = await FindByMobileAsync(mobileNumber, cancellationToken);
        return found is not null && found.Id != userId;
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
