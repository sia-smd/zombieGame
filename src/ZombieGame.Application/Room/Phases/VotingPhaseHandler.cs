namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Application.Common;
using ZombieGame.Application.Room.Bots;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public sealed class VotingPhaseHandler : IRoomPhaseHandler
{
    public RoomPhase Phase => RoomPhase.Voting;

    public Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        foreach (var player in context.Room.Players)
        {
            var sessionPlayer = context.Room.Session.GetPlayer(player.UserId);
            if (sessionPlayer is not null)
                player.IsAlive = sessionPlayer.IsAlive;
        }

        context.Room.Votes.Clear();
        context.Room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.VotingPhaseSeconds);
        RoomBotScheduler.ScheduleVotingBots(context.Room, context.Settings);
        return Task.FromResult(RoomTransitionResult.Stay("Voting started.", new PhaseChangedEvent(RoomPhase.Voting, "Voting started.")));
    }

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        var required = PhaseReadyService.GetRequiredForPhase(room);
        if (PhaseReadyService.IsAllReady(room, required))
            return Task.FromResult(RoomTransitionResult.Go(RoomPhase.VoteResult, "All players ready."));

        var timedOut = room.PhaseEndsAt is not null && DateTime.UtcNow >= room.PhaseEndsAt;
        var allVoted = AllAliveVoted(room);

        if (!timedOut && !allVoted)
            return Task.FromResult(RoomTransitionResult.Stay());

        return Task.FromResult(RoomTransitionResult.Go(RoomPhase.VoteResult, "Voting closed."));
    }

    public Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default)
    {
        if (command is MarkPhaseReadyCommand ready)
        {
            var required = PhaseReadyService.GetRequiredForPhase(context.Room);
            if (PhaseReadyService.MarkReady(context.Room, ready.UserId, required))
                return Task.FromResult(RoomTransitionResult.Go(RoomPhase.VoteResult, "All players ready."));
            return Task.FromResult(RoomTransitionResult.Stay("Ready recorded."));
        }

        if (command is not CastVoteCommand vote)
            return Task.FromResult(RoomTransitionResult.Stay("Invalid command for voting."));

        var room = context.Room;
        var voter = room.GetPlayer(vote.UserId)
            ?? throw new ServiceException("Player not found.");

        if (!voter.IsAlive)
            throw new ServiceException("Dead players cannot vote.");

        if (vote.TargetUserId != Guid.Empty)
        {
            var target = room.GetPlayer(vote.TargetUserId)
                ?? throw new ServiceException("Target not found.");

            if (!target.IsAlive)
                throw new ServiceException("Cannot vote for a dead player.");
        }

        var maxVotes = context.Settings.MaxVotesPerPlayer;
        var currentVotes = room.Votes.Count(v => v.Key == vote.UserId);
        if (maxVotes > 0 && currentVotes >= maxVotes && !room.Votes.ContainsKey(vote.UserId))
            throw new ServiceException("Maximum votes reached.");

        room.Votes[vote.UserId] = vote.TargetUserId;
        room.Statistics.TotalVotes++;
        RoomActivityTracker.MarkActed(room, vote.UserId);

        if (AllAliveVoted(room))
            return Task.FromResult(RoomTransitionResult.Go(
                RoomPhase.VoteResult,
                "All votes cast.",
                new VoteUpdatedEvent(room.Votes.Count)));

        return Task.FromResult(RoomTransitionResult.Stay(
            "Vote recorded.",
            new VoteUpdatedEvent(room.Votes.Count)));
    }

    private static bool AllAliveVoted(RoomState room)
    {
        var alive = room.AlivePlayers.Select(p => p.UserId).ToHashSet();
        return alive.Count > 0 && alive.All(id => room.Votes.ContainsKey(id));
    }
}
