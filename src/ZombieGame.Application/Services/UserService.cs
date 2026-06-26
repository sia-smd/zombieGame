namespace ZombieGame.Application.Services;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Auth;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Interfaces;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPlayerProfileRepository _profileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly GameSettings _gameSettings;
    private readonly AccountSettings _accountSettings;

    public UserService(
        IUserRepository userRepository,
        IPlayerProfileRepository profileRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<GameSettings> gameSettings,
        IOptions<AccountSettings> accountSettings)
    {
        _userRepository = userRepository;
        _profileRepository = profileRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _gameSettings = gameSettings.Value;
        _accountSettings = accountSettings.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            throw new ServiceException("Username and phone number are required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ServiceException("Password must be at least 6 characters.");

        if (await _userRepository.ExistsByUsernameAsync(request.Username, cancellationToken))
            throw new ServiceException("Username is already taken.");

        if (await _userRepository.ExistsByPhoneNumberAsync(request.PhoneNumber, cancellationToken))
            throw new ServiceException("Phone number is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = request.Email?.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            AccountType = Domain.Enums.AccountType.Mobile,
            MobileVerified = true,
            Coins = _gameSettings.StartingCoins,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _profileRepository.AddAsync(new PlayerProfile
        {
            PlayerId = user.Id,
            Name = user.Username,
            ImageId = _accountSettings.DefaultAvatarId,
            Level = 1
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _tokenService.GenerateAccessToken(user.Id, user.Username);
        return new AuthResponse(user.Id, user.Username, token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber.Trim(), cancellationToken)
            ?? throw new ServiceException("Invalid phone number or password.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new ServiceException("Invalid phone number or password.");

        user.LastLoginAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _tokenService.GenerateAccessToken(user.Id, user.Username);
        return new AuthResponse(user.Id, user.Username, token);
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException("User not found.");

        return new UserProfileResponse(
            user.Id,
            user.Username,
            user.Email,
            user.PhoneNumber ?? string.Empty,
            user.Coins,
            user.Wins,
            user.Losses,
            user.CreatedAt);
    }
}
