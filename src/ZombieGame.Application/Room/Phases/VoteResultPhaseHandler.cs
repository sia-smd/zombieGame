namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public sealed class VoteResultPhaseHandler : IRoomPhaseHandler
{
    private readonly IVotingService _voting;
    private readonly IWinConditionService _winConditions;
    private readonly IDayEventService _dayEvents;

    public VoteResultPhaseHandler(
        IVotingService voting,
        IWinConditionService winConditions,
        IDayEventService dayEvents)
    {
        _voting = voting;
        _winConditions = winConditions;
        _dayEvents = dayEvents;
    }

    public RoomPhase Phase => RoomPhase.VoteResult;

    public Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        room.Session.Votes.Clear();
        foreach (var (voterId, targetId) in room.Votes)
            room.Session.Votes[voterId] = targetId;

        var eliminatedId = _voting.ResolveElimination(room.Session);

        RoomEvent[] events;
        if (eliminatedId is Guid id)
        {
            var eliminated = room.GetPlayer(id);
            if (eliminated is not null)
            {
                eliminated.IsAlive = false;
                var sessionPlayer = room.Session.GetPlayer(id);
                if (sessionPlayer is not null)
                    sessionPlayer.IsAlive = false;
            }

            room.LastEliminatedPlayerId = id;
            room.Statistics.TotalEliminations++;
            events = [new PlayerEliminatedEvent(id)];
        }
        else
        {
            room.LastEliminatedPlayerId = null;
            events = [];
        }

        var winTeam = _winConditions.Evaluate(room.Session);
        if (winTeam is not null)
        {
            room.WinTeam = winTeam.Value;
            room.Session.WinTeam = winTeam.Value;
            room.NextDayNumber = null;
            room.NextDayEvent = null;
            return Task.FromResult(RoomTransitionResult.Go(
                RoomPhase.Finished,
                "Game finished.",
                events.Append(new GameFinishedEvent(winTeam.Value)).ToArray()));
        }

        var nextDay = room.DayNumber + 1;
        room.NextDayNumber = nextDay;
        room.NextDayEvent = nextDay > 1
            ? _dayEvents.PickRandomEvent()
            : DayEventType.NormalDay;
        room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.VoteResultDisplaySeconds);
        return Task.FromResult(RoomTransitionResult.Stay("Vote result published.", events));
    }

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room.PhaseEndsAt is null || DateTime.UtcNow < context.Room.PhaseEndsAt)
            return Task.FromResult(RoomTransitionResult.Stay());

        context.Room.DayNumber++;
        return Task.FromResult(RoomTransitionResult.Go(
            RoomPhase.DayStart,
            $"Day {context.Room.DayNumber} starting.",
            new PhaseChangedEvent(RoomPhase.DayStart, "Next day.")));
    }

    public Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay("Vote results are being displayed."));
}
