namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Domain.Enums;

/// <summary>
/// Terminal room phase. Persistence and cleanup are handled by the lifecycle
/// coordinator after the final state has been broadcast.
/// </summary>
public sealed class FinishedPhaseHandler : IRoomPhaseHandler
{
    public RoomPhase Phase => RoomPhase.Finished;

    public Task<RoomTransitionResult> OnEnterAsync(
        RoomContext context,
        CancellationToken cancellationToken = default)
    {
        context.Room.PhaseEndsAt = null;
        return Task.FromResult(RoomTransitionResult.Stay("Game finished."));
    }

    public Task<RoomTransitionResult> OnTickAsync(
        RoomContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay());

    public Task<RoomTransitionResult> HandleAsync(
        RoomContext context,
        IRoomCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay("The match has finished."));
}
