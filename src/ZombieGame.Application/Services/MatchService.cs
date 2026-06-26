namespace ZombieGame.Application.Services;

using ZombieGame.Application.DTOs.Matchmaking;
using ZombieGame.Application.Interfaces;
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

    public async Task<MatchSummaryResponse?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);
        if (match is null) return null;

        return new MatchSummaryResponse(
            match.Id,
            match.Status,
            match.CurrentPhase,
            match.SessionToken,
            match.Players.Count,
            match.MaxPlayers,
            match.CreatedAt);
    }

    public async Task<IReadOnlyList<MatchPlayerResponse>> GetMatchPlayersAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);
        if (match is null) return Array.Empty<MatchPlayerResponse>();

        var responses = new List<MatchPlayerResponse>();
        foreach (var player in match.Players.OrderBy(p => p.SeatIndex))
        {
            var user = await _userRepository.GetByIdAsync(player.UserId, cancellationToken);
            responses.Add(new MatchPlayerResponse(
                player.UserId,
                user?.Username ?? "Unknown",
                player.IsBot,
                player.IsAlive,
                player.SeatIndex,
                player.Role));
        }

        return responses;
    }
}
