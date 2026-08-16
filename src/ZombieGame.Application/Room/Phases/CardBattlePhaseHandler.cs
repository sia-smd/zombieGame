namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Application.Common;
using ZombieGame.Application.Room.Battle;
using ZombieGame.Application.Room.Bots;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public sealed class CardBattlePhaseHandler : IRoomPhaseHandler
{
    private readonly IBattleService _battles;
    private readonly IRoomStatePresenter _presenter;

    public CardBattlePhaseHandler(IBattleService battles, IRoomStatePresenter presenter)
    {
        _battles = battles;
        _presenter = presenter;
    }

    public RoomPhase Phase => RoomPhase.CardBattle;

    public async Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        await _battles.StartBattlesForDayAsync(
            context.Room,
            TimeSpan.FromSeconds(context.Settings.CardBattleSeconds),
            cancellationToken);

        // Drives the shared battle countdown clients render for the phase.
        context.Room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.CardBattleSeconds);

        RoomBotScheduler.ScheduleCardBattleBots(context.Room, context.Settings);

        var events = context.Room.BattlePairs
            .Select(p => (RoomEvent)new BattleStartedEvent(p))
            .ToArray();

        return RoomTransitionResult.Stay("Card battles started.", events);
    }

    public async Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;

        if (AllPairsFinished(room))
            return RoomTransitionResult.Go(RoomPhase.BattleResult, "All battles finished.");

        await _battles.AutoPassExpiredAsync(room, cancellationToken);

        if (AllPairsFinished(room))
            return RoomTransitionResult.Go(RoomPhase.BattleResult, "All battles finished.");

        return RoomTransitionResult.Stay();
    }

    public async Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default)
    {
        if (command is MarkPhaseReadyCommand ready)
            return HandleReady(context.Room, ready.UserId);

        return command switch
        {
            BattlePlayCardCommand play => await HandlePlayCardAsync(context, play, cancellationToken),
            BattlePassCommand pass => await HandlePassAsync(context.Room, pass, cancellationToken),
            _ => RoomTransitionResult.Stay("Invalid command for card battle.")
        };
    }

    private async Task<RoomTransitionResult> HandlePlayCardAsync(RoomContext context, BattlePlayCardCommand command, CancellationToken cancellationToken)
    {
        var room = context.Room;
        var battle = await _battles.PlayCardAsync(room, command.PairId, command.UserId, command.CardId, command.TargetUserId, cancellationToken);
        var pair = room.BattlePairs.FirstOrDefault(p => p.PairId == command.PairId)
            ?? throw new ServiceException("Battle is no longer active.");
        await _battles.SyncPairFromBattle(pair, battle);
        await _presenter.PushPrivateToPairAsync(room, command.PairId, cancellationToken);

        if (AllPairsFinished(room))
            return RoomTransitionResult.Go(RoomPhase.BattleResult, "All battles finished.");

        // Two actions per battle turn: re-arm the bot if it still has an action point left.
        if (!RoomBotScheduler.IsBotFinishedInPair(pair, command.UserId))
        {
            RoomBotScheduler.ScheduleNextBattleAction(room, command.UserId, context.Settings);
            return RoomTransitionResult.Stay("Card played. One action left.");
        }

        return RoomTransitionResult.Stay("Card played.");
    }

    private async Task<RoomTransitionResult> HandlePassAsync(RoomState room, BattlePassCommand command, CancellationToken cancellationToken)
    {
        var battle = await _battles.PassAsync(room, command.PairId, command.UserId, cancellationToken);
        var pair = room.BattlePairs.FirstOrDefault(p => p.PairId == command.PairId)
            ?? throw new ServiceException("Battle is no longer active.");
        await _battles.SyncPairFromBattle(pair, battle);
        await _presenter.PushPrivateToPairAsync(room, command.PairId, cancellationToken);

        if (AllPairsFinished(room))
            return RoomTransitionResult.Go(RoomPhase.BattleResult, "All battles finished.");

        return RoomTransitionResult.Stay("Pass recorded.");
    }

    private RoomTransitionResult HandleReady(RoomState room, Guid userId)
    {
        var required = PhaseReadyService.GetRequiredForPhase(room);
        if (PhaseReadyService.MarkReady(room, userId, required) && AllPairsFinished(room))
            return RoomTransitionResult.Go(RoomPhase.BattleResult, "All battles finished.");

        return RoomTransitionResult.Stay("Ready recorded.");
    }

    private static bool AllPairsFinished(RoomState room) =>
        room.BattlePairs.Count == 0 ||
        room.BattlePairs.All(p =>
            p.Status == BattlePairStatus.Finished
            || (p.BattleSession.Player1Finished && p.BattleSession.Player2Finished));
}
