namespace ZombieGame.Application.Services;

using ZombieGame.Application.DTOs.Matchmaking;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public class MatchService : IMatchService
{
    private readonly IMatchRepository _matchRepository;
    private readonly IUserRepository _userRepository;

    public MatchService(IMatchRepository matchRepository, IUserRepository userRepository)
    {
        _matchRepository = matchRepository;
        _userRepository = userRepository;
    }

    public async Task<MatchSummaryResponse?> GetMatchAsync(
        Guid requesterId,
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);
        if (match is null || !match.Players.Any(player => player.UserId == requesterId))
            return null;

        return new MatchSummaryResponse(
            match.Id,
            match.Status,
            match.CurrentPhase,
            match.Players.Count,
            match.MaxPlayers,
            match.CreatedAt,
            match.Name);
    }

    public async Task<IReadOnlyList<MatchPlayerResponse>?> GetMatchPlayersAsync(
        Guid requesterId,
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);
        if (match is null || !match.Players.Any(player => player.UserId == requesterId))
            return null;

        var responses = new List<MatchPlayerResponse>();
        var revealRoles = match.Status == MatchStatus.Finished;
        foreach (var player in match.Players.OrderBy(p => p.SeatIndex))
        {
            var user = await _userRepository.GetByIdAsync(player.UserId, cancellationToken);
            responses.Add(new MatchPlayerResponse(
                player.UserId,
                user?.Username ?? "Unknown",
                player.IsBot,
                player.IsAlive,
                player.SeatIndex,
                revealRoles || player.UserId == requesterId
                    ? player.Role
                    : PlayerRole.Unknown));
        }

        return responses;
    }
}
