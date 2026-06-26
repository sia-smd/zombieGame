namespace ZombieGame.Application.DTOs.Matchmaking;

using ZombieGame.Domain.Enums;

public record JoinQueueResponse(bool Queued, string Message, Guid? MatchId = null, string? SessionToken = null);

public record MatchSummaryResponse(
    Guid MatchId,
    MatchStatus Status,
    GamePhase CurrentPhase,
    string SessionToken,
    int PlayerCount,
    int MaxPlayers,
    DateTime CreatedAt);

public record MatchPlayerResponse(
    Guid UserId,
    string Username,
    bool IsBot,
    bool IsAlive,
    int SeatIndex,
    PlayerRole Role);
