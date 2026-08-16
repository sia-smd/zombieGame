namespace ZombieGame.Application.Room.Phases;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public sealed class DayStartPhaseHandler : IRoomPhaseHandler
{
    private readonly IRoleAssignmentService _roles;
    private readonly ICardDealingService _dealing;
    private readonly IDayEventService _dayEvents;
    private readonly IWinConditionService _winConditions;
    private readonly ICoinService _coinService;
    private readonly GameSettings _gameSettings;

    public DayStartPhaseHandler(
        IRoleAssignmentService roles,
        ICardDealingService dealing,
        IDayEventService dayEvents,
        IWinConditionService winConditions,
        ICoinService coinService,
        IOptions<GameSettings> gameSettings)
    {
        _roles = roles;
        _dealing = dealing;
        _dayEvents = dayEvents;
        _winConditions = winConditions;
        _coinService = coinService;
        _gameSettings = gameSettings.Value;
    }

    public RoomPhase Phase => RoomPhase.DayStart;

    public async Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        var session = room.Session;

        if (room.DayNumber == 1)
        {
            session.CurrentPhase = GamePhase.Day;
            session.TurnNumber = 1;
            session.Votes.Clear();
            session.FriendlyFireCount = 0;
            session.WinTeam = WinTeam.None;
            session.CurrentDayEvent = DayEventType.NormalDay;
            session.Metadata["dayEvent"] = session.CurrentDayEvent.ToString();
            _roles.AssignRoles(session, room.Players.Count);
            _dealing.InitializeHands(session);
            ResetActionPoints(session);
            BotObservationRecorder.InitializeBots(session, Random.Shared);

            await _coinService.CollectMatchEntryFeesAsync(
                room.MatchId,
                session.Players.Where(p => !p.IsBot).Select(p => p.UserId),
                cancellationToken);
        }
        else
        {
            session.TurnNumber = room.DayNumber;
            session.CurrentPhase = GamePhase.Day;
            session.Votes.Clear();
            session.CurrentDayEvent = _dayEvents.PickRandomEvent();
            session.Metadata["dayEvent"] = session.CurrentDayEvent.ToString();
            ResetDailyCombat(session);
            ResetActionPoints(session);
            _dealing.ReplenishInventory(session);
        }

        var afkRemoved = room.DayNumber == 1
            ? Array.Empty<RoomPlayerState>()
            : RoomActivityTracker.CloseDay(room, context.Settings.AfkDayLimit).ToArray();

        foreach (var idle in afkRemoved)
            EliminateForAfk(room, idle);

        room.CurrentDayEvent = session.CurrentDayEvent;
        room.PendingInvitations.Clear();
        room.BattlePairs.Clear();
        room.CurrentDayBattleSummaries.Clear();
        room.DaySummary = new DaySummaryState();
        room.RolesAtDayStart = session.Players.ToDictionary(p => p.UserId, p => p.Role);

        foreach (var player in room.Players)
        {
            player.HasSentInvitationToday = false;
            player.IsReady = false;
            player.IsResting = false;
        }

        // Hold on DayStart so clients can show day / alive count / countdown (Room Flow V2).
        room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.DayStartSeconds);

        var events = new List<RoomEvent> { new DayStartedEvent(room.DayNumber, session.CurrentDayEvent) };
        events.AddRange(afkRemoved.Select(p => (RoomEvent)new PlayerEliminatedEvent(p.UserId)));

        if (afkRemoved.Length > 0 && _winConditions.Evaluate(session) is WinTeam winTeam)
        {
            room.WinTeam = winTeam;
            session.WinTeam = winTeam;
            events.Add(new GameFinishedEvent(winTeam));
            return RoomTransitionResult.Go(
                RoomPhase.Finished,
                "Game finished.",
                events.ToArray());
        }

        return RoomTransitionResult.Stay(
            $"Day {room.DayNumber} started.",
            events.ToArray());
    }

    /// <summary>AFK protection: idle players are removed from the match (Room Flow V2).</summary>
    private static void EliminateForAfk(RoomState room, RoomPlayerState player)
    {
        player.IsAlive = false;
        player.IsResting = false;

        var sessionPlayer = room.Session.GetPlayer(player.UserId);
        if (sessionPlayer is not null)
            sessionPlayer.IsAlive = false;

        room.Statistics.TotalEliminations++;
    }

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        if (room.PhaseEndsAt is null || DateTime.UtcNow < room.PhaseEndsAt)
            return Task.FromResult(RoomTransitionResult.Stay());

        return Task.FromResult(RoomTransitionResult.Go(
            RoomPhase.OpponentSelection,
            "Day start complete.",
            new PhaseChangedEvent(RoomPhase.OpponentSelection, "Select your opponent.")));
    }

    public Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay("Day start is automatic."));

    private static void ResetDailyCombat(GameSessionState session)
    {
        foreach (var player in session.AlivePlayers)
        {
            player.ShotgunHitCount = 0;
            player.HasRevealedThisDay = false;
            if (player.Role == PlayerRole.PowerZombie)
                player.RemainingHealth = 2;
        }
    }

    private void ResetActionPoints(GameSessionState session)
    {
        foreach (var player in session.AlivePlayers)
        {
            player.ActionsUsedThisTurn = 0;
            player.PassesUsedThisDay = 0;
            player.ActionsPerTurn = _gameSettings.ActionsPerTurn;
        }
    }
}
