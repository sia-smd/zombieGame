namespace ZombieGame.Application.DTOs.Room;

using ZombieGame.Application.Room;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public record RoomStateDto(
    Guid MatchId,
    RoomPhase CurrentPhase,
    int DayNumber,
    DateTime? PhaseEndsAt,
    IReadOnlyList<RoomPlayerDto> Players,
    IReadOnlyList<BattlePairDto> BattlePairs,
    IReadOnlyList<BattleSummaryDto> BattleSummaries,
    IReadOnlyList<Guid> AvailableOpponents,
    IReadOnlyList<RoomInvitationDto> PendingInvitations,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> AvailableOpponentsByPlayer,
    bool VotesRevealed,
    IReadOnlyDictionary<Guid, int>? VoteCounts,
    DayEventType CurrentDayEvent,
    string? RoomName = null,
    int MaxPlayers = 8,
    bool FillWithBots = false,
    Guid? HostUserId = null,
    DateTime? BotsJoinAt = null,
    int BotFillTimeoutSeconds = 10,
    int InvitationTimeoutSeconds = 10,
    DaySummaryDto? DaySummary = null,
    RoomMeDto? Me = null,
    Guid? LastEliminatedPlayerId = null,
    WinTeam WinTeam = WinTeam.None,
    RoomMood RoomMood = RoomMood.Safe,
    int SnapshotVersion = 0,
    int PhaseSecondsRemaining = 0);

/// <summary>
/// The viewer's own private battle data (hand, health, actions). Only ever populated on
/// caller-scoped responses so hands never leak through group broadcasts.
/// </summary>
public record RoomMeDto(
    Guid UserId,
    PlayerRole Role,
    Guid RoleCardId,
    int Health,
    int MaxHealth,
    int ActionsPerTurn,
    int RemainingActions,
    IReadOnlyList<Guid> InventoryCardIds,
    Guid? PairId,
    Guid? OpponentId,
    IReadOnlyList<Guid> PlayedCardIds = null!,
    IReadOnlyList<Guid> OpponentPlayedCardIds = null!,
    bool OpponentFinished = false,
    bool BattleFinished = false,
    Guid? MyVoteTargetId = null);

public record RoomPlayerDto(
    Guid UserId,
    string Username,
    string ImageId,
    bool IsAlive,
    bool IsBot,
    int SeatIndex,
    bool IsPaired,
    bool HasSentInvitationToday,
    bool HasPendingInvitation,
    bool IsResting,
    bool IsDisconnected,
    RoomPlayerActivity Activity);

/// <summary>Public day outcomes — no cards or roles.</summary>
public record DaySummaryDto(
    IReadOnlyList<Guid> EliminatedPlayerIds,
    IReadOnlyList<Guid> NewlyInfectedPlayerIds,
    IReadOnlyList<Guid> RestingPlayerIds,
    int AliveCount);

/// <summary>Unanswered invitation, visible to the whole room so each client can react to its own.</summary>
public record RoomInvitationDto(
    Guid Id,
    Guid FromUserId,
    string FromUsername,
    string FromImageId,
    Guid ToUserId,
    string ToUsername,
    DateTime SentAt,
    DateTime? ExpiresAt);

public record BattlePairDto(
    Guid PairId,
    Guid Player1Id,
    Guid Player2Id,
    BattlePairStatus Status,
    BattlePublicAction Player1Summary,
    BattlePublicAction Player2Summary);

public record BattleSummaryDto(
    int DayNumber,
    Guid Player1Id,
    string Player1Name,
    BattlePublicAction Player1Action,
    Guid Player2Id,
    string Player2Name,
    BattlePublicAction Player2Action);

public record BattleInvitationDto(
    Guid Id,
    Guid FromUserId,
    Guid ToUserId,
    BattleInvitationStatus Status);

public record ChatMessageDto(Guid Id, Guid UserId, string Username, string Text, DateTime SentAt);

public record SendInvitationRequest(Guid TargetUserId);
public record RespondInvitationRequest(Guid InvitationId, bool Accept);
public record BattlePlayCardRequest(Guid PairId, Guid CardId, Guid? TargetUserId, string IdempotencyKey);
public record BattlePassRequest(Guid PairId, string IdempotencyKey);
public record SendChatRequest(string Text);
public record CastRoomVoteRequest(Guid TargetUserId, string IdempotencyKey);

public record RoomActionResult(bool Success, string Message, RoomStateDto? State = null);

public static class RoomStateMapper
{
    public static RoomStateDto ToPublicDto(
        RoomState room,
        Guid viewerId,
        IReadOnlySet<Guid>? availableOpponents = null,
        bool revealVotes = false,
        int botFillTimeoutSeconds = 10,
        int invitationTimeoutSeconds = 10,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>? availableOpponentsByPlayer = null,
        bool includePrivate = false)
    {
        var alive = room.AlivePlayers.ToList();
        var byPlayer = availableOpponentsByPlayer
            ?? new Dictionary<Guid, IReadOnlyList<Guid>>();

        var opponents = availableOpponents?.ToList()
            ?? (byPlayer.TryGetValue(viewerId, out var viewerOpponents)
                ? viewerOpponents.ToList()
                : alive.Where(p => p.UserId != viewerId).Select(p => p.UserId).ToList());

        DateTime? botsJoinAt = null;
        if (room.FillWithBots && room.CurrentPhase == RoomPhase.Lobby && room.Players.Count < room.MaxPlayers)
            botsJoinAt = room.CreatedAt.AddSeconds(botFillTimeoutSeconds);

        return new RoomStateDto(
            room.MatchId,
            room.CurrentPhase,
            room.DayNumber,
            ToUtc(room.PhaseEndsAt),
            room.Players.Select(p => new RoomPlayerDto(
                p.UserId,
                p.Username,
                string.IsNullOrWhiteSpace(p.ImageId) ? "avatar_default_01" : p.ImageId,
                p.IsAlive,
                p.IsBot,
                p.SeatIndex,
                room.IsPaired(p.UserId),
                p.HasSentInvitationToday,
                room.HasPendingInvitation(p.UserId),
                p.IsResting,
                p.IsDisconnected,
                RoomActivityTracker.GetActivity(room, p))).ToList(),
            room.BattlePairs.Select(p => new BattlePairDto(
                p.PairId,
                p.Player1Id,
                p.Player2Id,
                p.Status,
                p.Player1Summary,
                p.Player2Summary)).ToList(),
            room.CurrentDayBattleSummaries.Select(s => new BattleSummaryDto(
                s.DayNumber,
                s.Player1Id,
                s.Player1Name,
                s.Player1Action,
                s.Player2Id,
                s.Player2Name,
                s.Player2Action)).ToList(),
            opponents,
            BuildPendingInvitations(room),
            byPlayer,
            revealVotes,
            revealVotes ? BuildVoteCounts(room) : null,
            room.CurrentDayEvent,
            room.RoomName,
            room.MaxPlayers,
            room.FillWithBots,
            room.HostUserId,
            botsJoinAt,
            botFillTimeoutSeconds,
            invitationTimeoutSeconds,
            new DaySummaryDto(
                room.DaySummary.EliminatedPlayerIds,
                room.DaySummary.NewlyInfectedPlayerIds,
                room.DaySummary.RestingPlayerIds,
                room.DaySummary.AliveCount > 0 ? room.DaySummary.AliveCount : room.AlivePlayers.Count()),
            includePrivate ? BuildMe(room, viewerId) : null,
            room.LastEliminatedPlayerId,
            room.WinTeam,
            ComputeRoomMood(room),
            room.SnapshotVersion,
            SecondsRemaining(room.PhaseEndsAt));
    }

    internal static DateTime? ToUtc(DateTime? value)
    {
        if (value is null)
            return null;
        var utc = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    internal static int SecondsRemaining(DateTime? endsAt)
    {
        if (endsAt is null)
            return 0;

        var utc = ToUtc(endsAt)!.Value;
        return (int)Math.Max(0, Math.Ceiling((utc - DateTime.UtcNow).TotalSeconds));
    }

    /// <summary>
    /// Role-blind tension meter for the room panel / day summary (matches the design gauge).
    /// </summary>
    public static RoomMood ComputeRoomMood(RoomState room)
    {
        var total = Math.Max(1, room.Players.Count);
        var alive = room.AlivePlayers.Count();
        var aliveRatio = (double)alive / total;

        var eliminatedToday = room.DaySummary.EliminatedPlayerIds.Count;
        if (room.LastEliminatedPlayerId is Guid last &&
            !room.DaySummary.EliminatedPlayerIds.Contains(last))
            eliminatedToday++;

        var infectedToday = room.DaySummary.NewlyInfectedPlayerIds.Count;
        var pressure = eliminatedToday + infectedToday;

        if (pressure >= 3 || aliveRatio <= 0.35 || alive <= 2)
            return RoomMood.Critical;
        if (pressure >= 2 || aliveRatio <= 0.5)
            return RoomMood.Danger;
        if (pressure >= 1 || room.DayNumber >= 3)
            return RoomMood.Suspicious;
        return RoomMood.Safe;
    }

    private static RoomMeDto? BuildMe(RoomState room, Guid viewerId)
    {
        var sessionPlayer = room.Session.Players.FirstOrDefault(p => p.UserId == viewerId);
        if (sessionPlayer is null)
            return null;

        var hand = room.Session.PlayerHands.FirstOrDefault(h => h.UserId == viewerId);
        var inventory = new List<Guid>();
        if (hand?.InventorySlot1 is Guid s1) inventory.Add(s1);
        if (hand?.InventorySlot2 is Guid s2) inventory.Add(s2);
        if (hand?.InventorySlot3 is Guid s3) inventory.Add(s3);
        if (hand?.InventorySlot4 is Guid s4) inventory.Add(s4);

        var pair = room.BattlePairs.FirstOrDefault(p => p.Player1Id == viewerId || p.Player2Id == viewerId);
        Guid? opponentId = pair is null
            ? null
            : (pair.Player1Id == viewerId ? pair.Player2Id : pair.Player1Id);

        var maxHealth = sessionPlayer.Role == PlayerRole.PowerZombie ? 2 : 1;

        IReadOnlyList<Guid> played = Array.Empty<Guid>();
        IReadOnlyList<Guid> opponentPlayed = Array.Empty<Guid>();
        var opponentFinished = false;
        var battleFinished = false;

        if (pair is not null)
        {
            var isPlayer1 = pair.Player1Id == viewerId;
            played = isPlayer1 ? pair.Player1PlayedCardIds : pair.Player2PlayedCardIds;
            opponentFinished = isPlayer1
                ? pair.BattleSession.Player2Finished
                : pair.BattleSession.Player1Finished;
            battleFinished = pair.Status == BattlePairStatus.Finished
                || (pair.BattleSession.Player1Finished && pair.BattleSession.Player2Finished)
                || room.CurrentPhase is RoomPhase.BattleResult or RoomPhase.DaySummary;

            // Face-down until both players have finished playing.
            if (battleFinished)
                opponentPlayed = isPlayer1 ? pair.Player2PlayedCardIds : pair.Player1PlayedCardIds;
        }

        return new RoomMeDto(
            viewerId,
            sessionPlayer.Role,
            hand?.RoleCardId ?? Guid.Empty,
            sessionPlayer.RemainingHealth,
            maxHealth,
            sessionPlayer.ActionsPerTurn,
            sessionPlayer.RemainingActions,
            inventory,
            pair?.PairId,
            opponentId,
            played,
            opponentPlayed,
            opponentFinished,
            battleFinished,
            room.Votes.TryGetValue(viewerId, out var votedFor) ? votedFor : null);
    }

    private static List<RoomInvitationDto> BuildPendingInvitations(RoomState room) =>
        room.PendingInvitations
            .Where(i => i.Status == BattleInvitationStatus.Pending)
            .Select(i =>
            {
                var from = room.GetPlayer(i.FromUserId);
                return new RoomInvitationDto(
                    i.Id,
                    i.FromUserId,
                    from?.Username ?? string.Empty,
                    string.IsNullOrWhiteSpace(from?.ImageId) ? "avatar_default_01" : from.ImageId,
                    i.ToUserId,
                    room.GetPlayer(i.ToUserId)?.Username ?? string.Empty,
                    i.SentAt,
                    i.ExpiresAt);
            })
            .ToList();

    private static Dictionary<Guid, int> BuildVoteCounts(RoomState room) =>
        room.Votes.Values
            .Where(v => v != Guid.Empty)
            .GroupBy(v => v)
            .ToDictionary(g => g.Key, g => g.Count());
}
