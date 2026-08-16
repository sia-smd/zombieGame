namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Application.GameRules;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public sealed class BattleResultPhaseHandler : IRoomPhaseHandler
{
    private readonly IWinConditionService _winConditions;

    public BattleResultPhaseHandler(IWinConditionService winConditions) =>
        _winConditions = winConditions;

    public RoomPhase Phase => RoomPhase.BattleResult;

    public Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        room.CurrentDayBattleSummaries.Clear();

        foreach (var pair in room.BattlePairs)
        {
            var p1 = room.GetPlayer(pair.Player1Id);
            var p2 = room.GetPlayer(pair.Player2Id);
            room.CurrentDayBattleSummaries.Add(new BattleSummary
            {
                DayNumber = room.DayNumber,
                Player1Id = pair.Player1Id,
                Player1Name = p1?.Username ?? "?",
                Player1Action = pair.Player1Summary,
                Player2Id = pair.Player2Id,
                Player2Name = p2?.Username ?? "?",
                Player2Action = pair.Player2Summary
            });
        }

        SyncAliveFromSession(room);
        // Publish the aggregate battle outcomes on the Battle Summary screen.
        // There is no separate DaySummary stop before discussion.
        DaySummaryPhaseHandler.BuildDaySummary(room);

        var winTeam = _winConditions.Evaluate(room.Session);
        if (winTeam is not null)
        {
            room.WinTeam = winTeam.Value;
            room.Session.WinTeam = winTeam.Value;
            return Task.FromResult(RoomTransitionResult.Go(
                RoomPhase.Finished,
                "Game finished.",
                new GameFinishedEvent(winTeam.Value)));
        }

        room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.BattleResultDisplaySeconds);
        return Task.FromResult(RoomTransitionResult.Stay("Battle results published."));
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
        Task.FromResult(RoomTransitionResult.Stay("Battle results are being displayed."));

    private static void SyncAliveFromSession(RoomState room)
    {
        foreach (var player in room.Players)
        {
            var sessionPlayer = room.Session.GetPlayer(player.UserId);
            if (sessionPlayer is null)
                continue;

            var wasAlive = player.IsAlive;
            player.IsAlive = sessionPlayer.IsAlive;
            if (wasAlive && !player.IsAlive)
                DaySummaryPhaseHandler.TrackElimination(room, player.UserId);

            if (room.RolesAtDayStart.TryGetValue(player.UserId, out var startRole) &&
                startRole == PlayerRole.Human &&
                sessionPlayer.Role is PlayerRole.Zombie or PlayerRole.PowerZombie)
            {
                DaySummaryPhaseHandler.TrackInfection(room, player.UserId);
            }
        }
    }
}
