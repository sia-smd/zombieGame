namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.DTOs.Profile;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public interface IPlayerProgressService
{
    Task ApplyMatchDeltasAsync(
        GameSessionState state,
        WinTeam winningTeam,
        CancellationToken cancellationToken = default);

    Task<PlayerProgressResponse> GetProgressAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);
}
