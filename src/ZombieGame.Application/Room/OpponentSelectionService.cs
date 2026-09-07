namespace ZombieGame.Application.Room;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public sealed class OpponentSelectionService
{
    /// <summary>
    /// Opponents the player may invite right now. Everyone must be fought once before repeats,
    /// so previous opponents are excluded until the player has exhausted the roster.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> GetAvailableOpponentsAsync(
        RoomState room,
        Guid playerId,
        IBattlePairHistoryStore history,
        CancellationToken cancellationToken = default)
    {
        var candidates = GetInvitableCandidates(room, playerId);
        if (candidates.Count == 0)
            return candidates;

        var previous = await history.GetPreviousOpponentsAsync(room.MatchId, playerId, cancellationToken);
        var filtered = candidates.Where(id => !previous.Contains(id)).ToHashSet();

        if (filtered.Count == 0)
        {
            await history.ResetPlayerHistoryAsync(room.MatchId, playerId, cancellationToken);
            return candidates;
        }

        return filtered;
    }

    /// <summary>
    /// Same filtering as <see cref="GetAvailableOpponentsAsync"/> but never resets opponent history,
    /// so it is safe to call for every player while projecting room state.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> PeekAvailableOpponentsAsync(
        RoomState room,
        Guid playerId,
        IBattlePairHistoryStore history,
        CancellationToken cancellationToken = default)
    {
        var candidates = GetInvitableCandidates(room, playerId);
        if (candidates.Count == 0)
            return candidates;

        var previous = await history.GetPreviousOpponentsAsync(room.MatchId, playerId, cancellationToken);
        var filtered = candidates.Where(id => !previous.Contains(id)).ToHashSet();

        return filtered.Count == 0 ? candidates : filtered;
    }

    private static HashSet<Guid> GetInvitableCandidates(RoomState room, Guid playerId) =>
        room.AlivePlayers
            .Where(p => p.UserId != playerId)
            .Where(p => !room.IsPaired(p.UserId))
            .Where(p => !room.HasPendingInvitation(p.UserId))
            .Select(p => p.UserId)
            .ToHashSet();

    public BattlePair CreatePair(Guid player1Id, Guid player2Id)
    {
        var pairId = Guid.NewGuid();
        return new BattlePair
        {
            PairId = pairId,
            Player1Id = player1Id,
            Player2Id = player2Id,
            Status = BattlePairStatus.Pending,
            BattleSession = new PairBattleSession { PairId = pairId }
        };
    }

    /// <summary>
    /// At phase close: pair every leftover unmatched alive player into random 1v1s.
    /// At most one player rests (odd leftover). Works for bots and humans alike.
    /// </summary>
    public IReadOnlyList<BattlePair> PairAllUnmatched(RoomState room)
    {
        var unmatched = GetUnmatchedPlayers(room).ToList();
        if (unmatched.Count < 2)
            return Array.Empty<BattlePair>();

        ExpirePendingInvitationsInvolving(room, unmatched);

        // Fisher–Yates so leftover resting is not always the same seat order.
        for (var i = unmatched.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (unmatched[i], unmatched[j]) = (unmatched[j], unmatched[i]);
        }

        var created = new List<BattlePair>();
        for (var i = 0; i + 1 < unmatched.Count; i += 2)
        {
            var pair = CreatePair(unmatched[i], unmatched[i + 1]);
            room.BattlePairs.Add(pair);
            created.Add(pair);
        }

        return created;
    }

    public bool AllAlivePlayersPaired(RoomState room)
    {
        var alive = room.AlivePlayers.ToList();
        if (alive.Count == 0)
            return true;

        var pairedCount = alive.Count(p => room.IsPaired(p.UserId));
        var unmatched = alive.Count - pairedCount;

        return unmatched <= 1;
    }

    public IReadOnlyList<Guid> GetUnmatchedPlayers(RoomState room) =>
        room.AlivePlayers.Where(p => !room.IsPaired(p.UserId)).Select(p => p.UserId).ToList();

    private static void ExpirePendingInvitationsInvolving(RoomState room, IReadOnlyList<Guid> playerIds)
    {
        var involved = playerIds.ToHashSet();
        foreach (var invitation in room.PendingInvitations.Where(i => i.Status == BattleInvitationStatus.Pending))
        {
            if (!involved.Contains(invitation.FromUserId) && !involved.Contains(invitation.ToUserId))
                continue;

            invitation.Status = BattleInvitationStatus.Expired;
            var sender = room.GetPlayer(invitation.FromUserId);
            if (sender is not null && !room.IsPaired(sender.UserId))
                sender.HasSentInvitationToday = false;
        }
    }
}
