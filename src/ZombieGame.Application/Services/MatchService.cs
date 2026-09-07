namespace ZombieGame.Application.Services;

using ZombieGame.Application.DTOs.Matchmaking;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public class MatchService : IMatchService
{
    private readonly IMatchRepository _matchRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMatchSummaryStore _summaryStore;

    public MatchService(
        IMatchRepository matchRepository,
        IUserRepository userRepository,
        IMatchSummaryStore summaryStore)
    {
        _matchRepository = matchRepository;
        _userRepository = userRepository;
        _summaryStore = summaryStore;
    }

    public async Task<MatchSummaryResponse?> GetMatchAsync(
        Guid requesterId,
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);
        if (match is null || !match.Players.Any(player => player.UserId == requesterId))
            return null;

        var summary = await _summaryStore.GetAsync(matchId, cancellationToken);
        var totalDays = match.TotalDays > 0 ? match.TotalDays : summary?.TotalDays ?? 0;
        var winningTeam = match.WinningTeam != WinTeam.None
            ? match.WinningTeam
            : summary?.WinningTeam ?? WinTeam.None;

        return new MatchSummaryResponse(
            match.Id,
            match.Status,
            match.CurrentPhase,
            match.Players.Count,
            match.MaxPlayers,
            match.CreatedAt,
            match.Name,
            winningTeam,
            totalDays);
    }

    public async Task<IReadOnlyList<MatchPlayerResponse>?> GetMatchPlayersAsync(
        Guid requesterId,
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var report = await GetMatchResultAsync(requesterId, matchId, cancellationToken);
        return report?.Players;
    }

    public async Task<MatchResultReportDto?> GetMatchResultAsync(
        Guid requesterId,
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken);
        if (match is null || !match.Players.Any(player => player.UserId == requesterId))
            return null;

        var summary = await _summaryStore.GetAsync(matchId, cancellationToken);
        var totalDays = match.TotalDays > 0 ? match.TotalDays : summary?.TotalDays ?? 0;
        var winningTeam = match.WinningTeam != WinTeam.None
            ? match.WinningTeam
            : summary?.WinningTeam ?? WinTeam.None;

        var revealRoles = match.Status == MatchStatus.Finished || winningTeam != WinTeam.None;
        var players = new List<MatchPlayerResponse>();
        foreach (var player in match.Players.OrderBy(p => p.SeatIndex))
        {
            var user = await _userRepository.GetByIdAsync(player.UserId, cancellationToken);
            var role = revealRoles || player.UserId == requesterId
                ? player.Role
                : PlayerRole.Unknown;

            players.Add(new MatchPlayerResponse(
                player.UserId,
                user?.Username ?? "Unknown",
                player.IsBot,
                player.IsAlive,
                player.SeatIndex,
                role));
        }

        return new MatchResultReportDto(
            match.Id,
            match.Status,
            winningTeam,
            totalDays,
            players);
    }
}
