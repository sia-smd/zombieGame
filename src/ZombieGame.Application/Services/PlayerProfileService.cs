namespace ZombieGame.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Profile;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Validation;
using ZombieGame.Domain.Interfaces;

public sealed class PlayerProfileService : IPlayerProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly IPlayerProfileRepository _profileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAvatarCatalogService _avatarCatalog;
    private readonly ProfileNameValidator _nameValidator;
    private readonly ILogger<PlayerProfileService> _logger;

    public PlayerProfileService(
        IUserRepository userRepository,
        IPlayerProfileRepository profileRepository,
        IUnitOfWork unitOfWork,
        IAvatarCatalogService avatarCatalog,
        IForbiddenWordsService forbiddenWords,
        IOptions<AccountSettings> accountSettings,
        ILogger<PlayerProfileService> logger)
    {
        _userRepository = userRepository;
        _profileRepository = profileRepository;
        _unitOfWork = unitOfWork;
        _avatarCatalog = avatarCatalog;
        _nameValidator = new ProfileNameValidator(forbiddenWords, accountSettings.Value.MaxProfileNameLength);
        _logger = logger;
    }

    public async Task<CurrentPlayerProfileResponse> GetCurrentPlayerAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithProfileAsync(playerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        return MapToResponse(user);
    }

    public async Task<CurrentPlayerProfileResponse> UpdateProfileAsync(
        Guid playerId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        _nameValidator.Validate(request.Name);

        if (!_avatarCatalog.IsAllowed(request.ImageId))
            throw new ServiceException("ImageId is not an allowed avatar.");

        var user = await _userRepository.GetByIdWithProfileAsync(playerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        var profile = user.Profile
            ?? throw new ServiceException("Player profile not found.");

        profile.Name = request.Name.Trim();
        profile.ImageId = request.ImageId.Trim();
        user.Username = profile.Name;
        _profileRepository.Update(profile);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated for player {PlayerId}", playerId);

        return MapToResponse(user);
    }

    private static CurrentPlayerProfileResponse MapToResponse(Domain.Entities.User user)
    {
        var profile = user.Profile ?? new Domain.Entities.PlayerProfile
        {
            PlayerId = user.Id,
            Name = user.Username,
            ImageId = "avatar_default_01",
            Level = 1
        };

        return new CurrentPlayerProfileResponse(
            user.Id,
            user.AccountType,
            profile.Name,
            profile.ImageId,
            profile.Level,
            user.Coins,
            Array.Empty<PlayerInventoryItemDto>(),
            new PlayerStatisticsDto(user.Wins, user.Losses, user.Wins + user.Losses),
            user.CreatedAt);
    }
}
