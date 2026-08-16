namespace ZombieGame.Application.Room;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

/// <summary>
/// Tracks per-day player activity for AFK protection and projects the activity badge
/// shown in the room player list (Room Flow V2).
/// </summary>
public static class RoomActivityTracker
{
    public static void MarkActed(RoomState room, Guid userId)
    {
        var player = room.GetPlayer(userId);
        if (player is null)
            return;

        player.HasActedToday = true;
        player.InactiveDayCount = 0;
    }

    public static void MarkConnected(RoomState room, Guid userId)
    {
        var player = room.GetPlayer(userId);
        if (player is not null)
            player.DisconnectedAt = null;
    }

    public static void MarkDisconnected(RoomState room, Guid userId, DateTime? at = null)
    {
        var player = room.GetPlayer(userId);
        if (player is not null && player.DisconnectedAt is null)
            player.DisconnectedAt = at ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Closes the previous day: idle players accumulate an inactive day and are removed once
    /// the limit is reached. Returns the players eliminated for being AFK.
    /// </summary>
    public static IReadOnlyList<RoomPlayerState> CloseDay(RoomState room, int afkDayLimit)
    {
        var removed = new List<RoomPlayerState>();

        foreach (var player in room.Players.Where(p => p.IsAlive && !p.IsBot))
        {
            if (player.HasActedToday)
            {
                player.InactiveDayCount = 0;
                continue;
            }

            player.InactiveDayCount++;
            if (afkDayLimit > 0 && player.InactiveDayCount >= afkDayLimit)
                removed.Add(player);
        }

        foreach (var player in room.Players)
            player.HasActedToday = false;

        return removed;
    }

    public static RoomPlayerActivity GetActivity(RoomState room, RoomPlayerState player)
    {
        if (!player.IsAlive)
            return RoomPlayerActivity.Eliminated;

        if (player.IsDisconnected)
            return RoomPlayerActivity.Disconnected;

        if (room.IsPaired(player.UserId))
            return RoomPlayerActivity.InBattle;

        if (player.IsResting)
            return RoomPlayerActivity.Resting;

        var pending = room.PendingInvitations
            .FirstOrDefault(i => i.Status == BattleInvitationStatus.Pending &&
                                 (i.FromUserId == player.UserId || i.ToUserId == player.UserId));

        if (pending is not null)
            return pending.FromUserId == player.UserId
                ? RoomPlayerActivity.Inviting
                : RoomPlayerActivity.Waiting;

        return RoomPlayerActivity.Available;
    }
}
