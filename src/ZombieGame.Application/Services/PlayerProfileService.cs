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
    public const string CustomAvatarId = "avatar_custom";

    private readonly IUserRepository _userRepository;
    private readonly IPlayerProfileRepository _profileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAvatarCatalogService _avatarCatalog;
    private readonly ProfileNameValidator _nameValidator;
    private readonly UsernameValidator _usernameValidator;
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
        _usernameValidator = new UsernameValidator(forbiddenWords, accountSettings.Value.MaxProfileNameLength);
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
        if (request.Name is null && request.ImageId is null)
            throw new ServiceException("Nothing to update.");

        var user = await _userRepository.GetByIdWithProfileAsync(playerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        var profile = user.Profile
            ?? throw new ServiceException("Player profile not found.");

        if (request.Name is not null)
        {
            _nameValidator.Validate(request.Name);
            profile.Name = request.Name.Trim();
        }

        if (request.ImageId is not null)
        {
            var imageId = request.ImageId.Trim();
            if (!IsAllowedCatalogAvatar(imageId))
                throw new ServiceException("ImageId is not an allowed avatar.");

            profile.ImageId = imageId;
            profile.CustomAvatarData = null;
        }

        _profileRepository.Update(profile);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated for player {PlayerId}", playerId);

        return MapToResponse(user);
    }

    public async Task<CurrentPlayerProfileResponse> UpdateUsernameAsync(
        Guid playerId,
        UpdateUsernameRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = _usernameValidator.Normalize(request.Username);

        var user = await _userRepository.GetByIdWithProfileAsync(playerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        if (!string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)
            && await _userRepository.ExistsByUsernameAsync(username, cancellationToken))
            throw new ServiceException("Username is already taken.");

        user.Username = username;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Username updated for player {PlayerId}", playerId);

        return MapToResponse(user);
    }

    public async Task<CurrentPlayerProfileResponse> UploadAvatarAsync(
        Guid playerId,
        UploadAvatarRequest request,
        CancellationToken cancellationToken = default)
    {
        var bytes = AvatarImageValidator.DecodeAndValidate(request.ImageBase64);
        var stored = Convert.ToBase64String(bytes);

        var user = await _userRepository.GetByIdWithProfileAsync(playerId, cancellationToken)
            ?? throw new ServiceException("Player not found.");

        var profile = user.Profile
            ?? throw new ServiceException("Player profile not found.");

        profile.ImageId = CustomAvatarId;
        profile.CustomAvatarData = stored;
        _profileRepository.Update(profile);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Custom avatar uploaded for player {PlayerId}", playerId);

        return MapToResponse(user);
    }

    public IReadOnlyList<AvatarOptionDto> GetAvatarOptions() =>
        _avatarCatalog.GetAllowedAvatarIds()
            .Where(id => !string.Equals(id, CustomAvatarId, StringComparison.OrdinalIgnoreCase))
            .Select(id => new AvatarOptionDto(id, FormatAvatarLabel(id)))
            .ToList();

    private bool IsAllowedCatalogAvatar(string imageId) =>
        _avatarCatalog.IsAllowed(imageId)
        && !string.Equals(imageId, CustomAvatarId, StringComparison.OrdinalIgnoreCase);

    private static string FormatAvatarLabel(string imageId) =>
        imageId.Replace("avatar_", "", StringComparison.OrdinalIgnoreCase)
            .Replace('_', ' ');

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
            user.Username,
            profile.ImageId,
            profile.CustomAvatarData,
            profile.Level,
            user.Coins,
            user.PhoneNumber is null ? null : MobileNumberValidator.Normalize(user.PhoneNumber),
            user.MobileVerified,
            user.PendingPhoneNumber is null ? null : MobileNumberValidator.Normalize(user.PendingPhoneNumber),
            user.AccountType == Domain.Enums.AccountType.Mobile,
            Array.Empty<PlayerInventoryItemDto>(),
            new PlayerStatisticsDto(user.Wins, user.Losses, user.Wins + user.Losses),
            user.CreatedAt);
    }
}
