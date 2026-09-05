namespace ZombieGame.Application.Room.Phases;

using ZombieGame.Application.Room.Battle;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

/// <summary>
/// Short barrier after opponent selection: assign Rest Mode, ensure battle rooms exist,
/// then countdown before simultaneous battles (Room Flow V2).
/// </summary>
public sealed class BattlePreparationPhaseHandler : IRoomPhaseHandler
{
    private readonly IBattleService _battles;
    private readonly IBattleStore _battleStore;

    public BattlePreparationPhaseHandler(IBattleService battles, IBattleStore battleStore)
    {
        _battles = battles;
        _battleStore = battleStore;
    }

    public RoomPhase Phase => RoomPhase.BattlePreparation;

    public async Task<RoomTransitionResult> OnEnterAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        room.PhaseEndsAt = DateTime.UtcNow.AddSeconds(context.Settings.BattlePreparationSeconds);
        AssignRestingPlayers(room);
        await EnsureBattlesExistAsync(room, context.Settings.CardBattleSeconds, cancellationToken);
        return RoomTransitionResult.Stay(
            "Preparing battles...",
            new PhaseChangedEvent(RoomPhase.BattlePreparation, "Preparing Battles..."));
    }

    public Task<RoomTransitionResult> OnTickAsync(RoomContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room.PhaseEndsAt is null || DateTime.UtcNow < context.Room.PhaseEndsAt)
            return Task.FromResult(RoomTransitionResult.Stay());

        return Task.FromResult(RoomTransitionResult.Go(
            RoomPhase.CardBattle,
            "Battles begin.",
            new PhaseChangedEvent(RoomPhase.CardBattle, "Battles begin.")));
    }

    public Task<RoomTransitionResult> HandleAsync(RoomContext context, IRoomCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(RoomTransitionResult.Stay("Battles are being prepared."));

    /// <summary>Every unpaired alive player rests today (normally the odd leftover after auto-pairing a final duo).</summary>
    public static void AssignRestingPlayers(RoomState room)
    {
        foreach (var player in room.Players)
            player.IsResting = false;

        foreach (var player in room.AlivePlayers.Where(p => !room.IsPaired(p.UserId)))
            player.IsResting = true;
    }

    private async Task EnsureBattlesExistAsync(RoomState room, int cardBattleSeconds, CancellationToken cancellationToken)
    {
        var duration = TimeSpan.FromSeconds(cardBattleSeconds);
        foreach (var pair in room.BattlePairs.ToList())
        {
            // Invitation accept already creates a battle with PairId == BattleId.
            // RandomAssignment CreatePair may leave a pair without a stored battle.
            var existing = await _battleStore.GetAsync(room.MatchId, pair.PairId, cancellationToken);
            if (existing is not null)
                continue;

            var battle = await _battles.CreateBattleAsync(room, pair.Player1Id, pair.Player2Id, duration, cancellationToken);
            var index = room.BattlePairs.IndexOf(pair);
            if (index >= 0)
                room.BattlePairs[index] = _battles.CreatePairReference(battle);
        }
    }
}
