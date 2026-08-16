namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

/// <summary>
/// Public day outcomes only: eliminations, infections, resting players, alive count (Room Flow V2).
/// </summary>
public sealed class DaySummaryPhaseHandler : IRoomPhaseHandler
{
    public RoomPhase Phase => RoomPhase.DaySummary;

    public Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        BuildDaySummary(room);
        room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.DaySummarySeconds);
        return Task.FromResult(RoomTransitionResult.Stay(
            "Day summary.",
            new PhaseChangedEvent(RoomPhase.DaySummary, "Day summary.")));
    }

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room.PhaseEndsAt is null || DateTime.UtcNow < context.Room.PhaseEndsAt)
            return Task.FromResult(RoomTransitionResult.Stay());

        return Task.FromResult(RoomTransitionResult.Go(
            RoomPhase.Discussion,
            "Discussion started.",
            new PhaseChangedEvent(RoomPhase.Discussion, "Discussion started.")));
    }

    public Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay("Day summary is being displayed."));

    public static void BuildDaySummary(RoomState room)
    {
        room.DaySummary = new DaySummaryState
        {
            EliminatedPlayerIds = room.DaySummary.EliminatedPlayerIds.Distinct().ToList(),
            NewlyInfectedPlayerIds = room.DaySummary.NewlyInfectedPlayerIds.Distinct().ToList(),
            RestingPlayerIds = room.RestingPlayers.Select(p => p.UserId).ToList(),
            AliveCount = room.AlivePlayers.Count()
        };
    }

    /// <summary>Called when syncing battle outcomes into room player flags.</summary>
    public static void TrackElimination(RoomState room, Guid playerId)
    {
        if (!room.DaySummary.EliminatedPlayerIds.Contains(playerId))
            room.DaySummary.EliminatedPlayerIds.Add(playerId);
        room.Statistics.TotalEliminations++;
    }

    public static void TrackInfection(RoomState room, Guid playerId)
    {
        if (!room.DaySummary.NewlyInfectedPlayerIds.Contains(playerId))
            room.DaySummary.NewlyInfectedPlayerIds.Add(playerId);
        room.Statistics.TotalInfections++;
    }
}
