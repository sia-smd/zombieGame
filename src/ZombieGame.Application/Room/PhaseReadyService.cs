namespace ZombieGame.Application.Room;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public static class PhaseReadyService
{
    public static bool MarkReady(RoomState room, Guid userId, IEnumerable<Guid> requiredPlayers)
    {
        if (!requiredPlayers.Contains(userId))
            return false;

        room.PhaseReadyPlayers.Add(userId);
        return IsAllReady(room, requiredPlayers);
    }

    public static bool IsAllReady(RoomState room, IEnumerable<Guid> requiredPlayers)
    {
        var required = requiredPlayers.ToHashSet();
        return required.Count > 0 && required.All(id => room.PhaseReadyPlayers.Contains(id));
    }

    public static void Clear(RoomState room) => room.PhaseReadyPlayers.Clear();

    public static IEnumerable<Guid> GetRequiredForPhase(RoomState room)
    {
        return room.CurrentPhase switch
        {
            RoomPhase.OpponentSelection => room.AlivePlayers
                .Where(p => !room.IsPaired(p.UserId))
                .Select(p => p.UserId),
            RoomPhase.CardBattle => room.BattlePairs
                .Where(p => p.Status == BattlePairStatus.InProgress)
                .SelectMany(p => new[] { p.Player1Id, p.Player2Id }),
            RoomPhase.Discussion or RoomPhase.Voting => room.AlivePlayers.Select(p => p.UserId),
            _ => Array.Empty<Guid>()
        };
    }
}
