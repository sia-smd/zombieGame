namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.DTOs.Profile;

public interface IPlayerProfileService
{
    Task<CurrentPlayerProfileResponse> GetCurrentPlayerAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task<CurrentPlayerProfileResponse> UpdateProfileAsync(
        Guid playerId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<CurrentPlayerProfileResponse> UpdateUsernameAsync(
        Guid playerId,
        UpdateUsernameRequest request,
        CancellationToken cancellationToken = default);

    Task<CurrentPlayerProfileResponse> UploadAvatarAsync(
        Guid playerId,
        UploadAvatarRequest request,
        CancellationToken cancellationToken = default);

    IReadOnlyList<AvatarOptionDto> GetAvatarOptions();
}
