namespace ZombieGame.Application.DTOs.Matchmaking;

using ZombieGame.Domain.Enums;

public record JoinQueueResponse(bool Queued, string Message, Guid? MatchId = null, string? SessionToken = null);

public record CreateRoomRequest(
    string RoomName,
    int MaxPlayers,
    bool FillWithBots = true);

public record CreateRoomResponse(
    Guid MatchId,
    string SessionToken,
    string RoomName,
    int MaxPlayers,
    bool FillWithBots,
    int EntryFeeCoins);

public record OpenRoomDto(
    Guid MatchId,
    string RoomName,
    int PlayerCount,
    int MaxPlayers,
    DateTime CreatedAt,
    string? HostUsername,
    string RoomCode,
    bool FillWithBots,
    int EntryFeeCoins);

public record JoinOpenRoomRequest(Guid? MatchId = null, string? RoomCode = null);

public record LeaveWaitingRoomRequest(Guid MatchId);

public record SendRoomInviteRequest(Guid MatchId, string Username);

public record SendRoomInviteResponse(Guid InviteId, DateTime ExpiresAt);

public record RespondRoomInviteRequest(Guid InviteId);

public record RoomInviteReceivedDto(
    Guid InviteId,
    Guid MatchId,
    string RoomName,
    string RoomCode,
    string FromUsername,
    DateTime ExpiresAt);

public record RoomInviteResolvedDto(
    Guid InviteId,
    Guid MatchId,
    bool Accepted,
    string ToUsername);

public record JoinOpenRoomResponse(
    Guid MatchId,
    string SessionToken,
    string RoomName,
    int MaxPlayers,
    bool FillWithBots,
    int EntryFeeCoins,
    string RoomCode);

public record RoomConfigResponse(
    int MinPlayers,
    int MaxPlayers,
    int DefaultMaxPlayers,
    int EntryFeeCoins,
    bool FillWithBotsDefault,
    int BotFillTimeoutSeconds);

public record MatchSummaryResponse(
    Guid MatchId,
    MatchStatus Status,
    GamePhase CurrentPhase,
    int PlayerCount,
    int MaxPlayers,
    DateTime CreatedAt,
    string? Name = null);

public record MatchPlayerResponse(
    Guid UserId,
    string Username,
    bool IsBot,
    bool IsAlive,
    int SeatIndex,
    PlayerRole Role);
