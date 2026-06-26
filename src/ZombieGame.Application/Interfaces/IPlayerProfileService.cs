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
}
