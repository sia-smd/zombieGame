namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.DTOs.Game;
using ZombieGame.Application.DTOs.Matchmaking;

public interface IMatchService
{
    Task<MatchSummaryResponse?> GetMatchAsync(Guid requesterId, Guid matchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchPlayerResponse>?> GetMatchPlayersAsync(Guid requesterId, Guid matchId, CancellationToken cancellationToken = default);
    Task<MatchResultReportDto?> GetMatchResultAsync(Guid requesterId, Guid matchId, CancellationToken cancellationToken = default);
}

public interface IGameService
{
    Task<GameActionResult> JoinMatchAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
    Task<GameActionResult> StartGameAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
    Task<GameActionResult> PlayCardAsync(Guid userId, Guid matchId, string sessionToken, PlayCardRequest request, CancellationToken cancellationToken = default);
    Task<GameActionResult> PassActionAsync(Guid userId, Guid matchId, string sessionToken, PassActionRequest request, CancellationToken cancellationToken = default);
    Task<GameActionResult> EndTurnAsync(Guid userId, Guid matchId, string sessionToken, EndTurnRequest request, CancellationToken cancellationToken = default);
    Task<GameActionResult> VotePlayerAsync(Guid userId, Guid matchId, string sessionToken, VotePlayerRequest request, CancellationToken cancellationToken = default);
    Task<GameStateDto?> GetStateAsync(Guid userId, Guid matchId, string sessionToken, CancellationToken cancellationToken = default);
    Task<GameActionResult> AdvancePhaseIfExpiredAsync(Guid matchId, CancellationToken cancellationToken = default);
}
