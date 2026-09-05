namespace ZombieGame.Application.Room.Battle;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public interface IBattleService
{
    Task<Battle> CreateBattleAsync(RoomState room, Guid playerA, Guid playerB, TimeSpan duration, CancellationToken cancellationToken = default);
    Task StartBattlesForDayAsync(RoomState room, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<Battle> PlayCardAsync(
        RoomState room,
        Guid battleId,
        Guid userId,
        Guid cardId,
        Guid? targetUserId,
        int? inventorySlotIndex = null,
        CancellationToken cancellationToken = default);
    Task<Battle> FinishTurnAsync(RoomState room, Guid battleId, Guid userId, CancellationToken cancellationToken = default);
    Task<Battle> PassAsync(RoomState room, Guid battleId, Guid userId, CancellationToken cancellationToken = default);
    Task AutoPassExpiredAsync(RoomState room, CancellationToken cancellationToken = default);
    Task SyncPairFromBattle(BattlePair pair, Battle battle);
    BattlePair CreatePairReference(Battle battle);
}

public sealed class BattleService : IBattleService
{
    private readonly IBattleStore _battleStore;
    private readonly IGameRulesEngine _rulesEngine;
    private readonly ICardRegistry _cardRegistry;
    private readonly ICardConsumptionService _consumption;
    private readonly ICardPlayValidator _cardValidator;
    private readonly RoomSettings _settings;
    private readonly GameSettings _gameSettings;

    public BattleService(
        IBattleStore battleStore,
        IGameRulesEngine rulesEngine,
        ICardRegistry cardRegistry,
        ICardConsumptionService consumption,
        ICardPlayValidator cardValidator,
        IOptions<RoomSettings> settings,
        IOptions<GameSettings> gameSettings)
    {
        _battleStore = battleStore;
        _rulesEngine = rulesEngine;
        _cardRegistry = cardRegistry;
        _consumption = consumption;
        _cardValidator = cardValidator;
        _settings = settings.Value;
        _gameSettings = gameSettings.Value;
    }

    public async Task<Battle> CreateBattleAsync(
        RoomState room,
        Guid playerA,
        Guid playerB,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var battle = new Battle
        {
            BattleId = Guid.NewGuid(),
            MatchId = room.MatchId,
            DayNumber = room.DayNumber,
            PlayerA = playerA,
            PlayerB = playerB,
            Status = BattleAggregateStatus.Pending
        };

        await _battleStore.SaveAsync(battle, cancellationToken);
        room.Statistics.TotalBattles++;
        return battle;
    }

    public async Task StartBattlesForDayAsync(RoomState room, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var endsAt = DateTime.UtcNow.Add(duration);
        foreach (var pair in room.BattlePairs)
        {
            var battle = await _battleStore.GetAsync(room.MatchId, pair.PairId, cancellationToken)
                ?? new Battle
                {
                    BattleId = pair.PairId,
                    MatchId = room.MatchId,
                    DayNumber = room.DayNumber,
                    PlayerA = pair.Player1Id,
                    PlayerB = pair.Player2Id
                };

            battle.Status = BattleAggregateStatus.InProgress;
            battle.BattleEndsAt = endsAt;
            battle.PlayerAFinished = false;
            battle.PlayerBFinished = false;
            battle.PlayerAPublicAction = BattlePublicAction.None;
            battle.PlayerBPublicAction = BattlePublicAction.None;
            battle.CardsPlayed.Clear();
            await _battleStore.SaveAsync(battle, cancellationToken);

            pair.Status = BattlePairStatus.InProgress;
            pair.BattleEndsAt = endsAt;
            pair.Player1Summary = BattlePublicAction.None;
            pair.Player2Summary = BattlePublicAction.None;
            pair.Player1PlayedCardIds.Clear();
            pair.Player2PlayedCardIds.Clear();
            pair.BattleSession.Player1Finished = false;
            pair.BattleSession.Player2Finished = false;
            pair.BattleSession.Player1Action = BattlePublicAction.None;
            pair.BattleSession.Player2Action = BattlePublicAction.None;

            ResetBattleTurn(room, pair.Player1Id);
            ResetBattleTurn(room, pair.Player2Id);
        }
    }

    private void ResetBattleTurn(RoomState room, Guid playerId)
    {
        var sessionPlayer = room.Session.GetPlayer(playerId);
        if (sessionPlayer is not null)
        {
            sessionPlayer.ActionsUsedThisTurn = 0;
            sessionPlayer.ActionsPerTurn = Math.Max(1, _gameSettings.ActionsPerTurn);
        }

        var roomPlayer = room.GetPlayer(playerId);
        if (roomPlayer is not null)
            roomPlayer.DisconnectedAt = null;
    }

    public async Task<Battle> PlayCardAsync(
        RoomState room,
        Guid battleId,
        Guid userId,
        Guid cardId,
        Guid? targetUserId,
        int? inventorySlotIndex = null,
        CancellationToken cancellationToken = default)
    {
        var battle = await RequireBattleAsync(room.MatchId, battleId, userId, cancellationToken);
        var card = _cardRegistry.GetById(cardId)
            ?? throw new ServiceException("Invalid card.");

        var hand = room.Session.PlayerHands.FirstOrDefault(h => h.UserId == userId)
            ?? throw new ServiceException("Player hand not found.");

        ResolveInventorySlot(hand, cardId, inventorySlotIndex, battle, userId);

        if (card.EffectKey.Equals("pass", StringComparison.OrdinalIgnoreCase))
            return await QueuePassCardAsync(room, battle, userId, cardId, inventorySlotIndex, cancellationToken);

        var resolvedTarget = ResolveTargetId(battle, userId, card, targetUserId);
        ValidateQueuedPlay(room, userId, hand, card, resolvedTarget);

        QueueCardPlay(battle, userId, cardId, inventorySlotIndex, resolvedTarget);
        ConsumeBattleAction(room, userId);

        room.Statistics.TotalCardsPlayed++;
        RecordPlayerAction(room, userId);
        MarkAsActive(room, userId);
        ApplyPublicAction(battle, userId, BattlePublicAction.Action);
        // Turn ends only via FinishTurnAsync (or timer/disconnect auto-pass), never when action points hit zero.

        await _battleStore.SaveAsync(battle, cancellationToken);
        return battle;
    }

    public async Task<Battle> FinishTurnAsync(
        RoomState room,
        Guid battleId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var battle = await RequireBattleAsync(room.MatchId, battleId, userId, cancellationToken);
        await CompletePlayerTurnAsync(room, battle, userId, cancellationToken);
        return battle;
    }

    public Task<Battle> PassAsync(
        RoomState room,
        Guid battleId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        FinishTurnAsync(room, battleId, userId, cancellationToken);

    public async Task AutoPassExpiredAsync(RoomState room, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var pair in room.BattlePairs.Where(p => p.Status == BattlePairStatus.InProgress))
        {
            var expired = pair.BattleEndsAt is not null && now >= pair.BattleEndsAt;
            var battle = await _battleStore.GetAsync(room.MatchId, pair.PairId, cancellationToken);
            if (battle is null)
                continue;

            var changed = false;
            foreach (var playerId in new[] { battle.PlayerA, battle.PlayerB })
            {
                if (HasFinished(battle, playerId))
                    continue;

                if (!expired && !IsGraceExpired(room, battle, playerId, now))
                    continue;

                await CompletePlayerTurnAsync(room, battle, playerId, cancellationToken);
                changed = true;
            }

            if (expired && !battle.IsFinished)
                battle.Status = BattleAggregateStatus.Finished;

            if (!changed && !expired)
                continue;

            await _battleStore.SaveAsync(battle, cancellationToken);
            await SyncPairFromBattle(pair, battle);
        }
    }

    private async Task CompletePlayerTurnAsync(
        RoomState room,
        Battle battle,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (HasFinished(battle, userId))
            return;

        var playedCards = battle.CardsPlayed.Where(c => c.PlayerId == userId).ToList();
        var action = playedCards.Count > 0 && playedCards.All(c => IsPassCard(c.CardId))
            ? BattlePublicAction.Pass
            : playedCards.Count > 0
                ? BattlePublicAction.Action
                : BattlePublicAction.Pass;

        MarkPlayerFinished(battle, userId, action);
        RecordPlayerAction(room, userId);
        MarkAsActive(room, userId);

        if (battle.IsFinished)
            await ResolveBattleAsync(room, battle, cancellationToken);

        await _battleStore.SaveAsync(battle, cancellationToken);
    }

    private async Task ResolveBattleAsync(RoomState room, Battle battle, CancellationToken cancellationToken)
    {
        if (battle.CardsPlayed.Count == 0)
        {
            battle.Status = BattleAggregateStatus.Finished;
            return;
        }

        var indexed = battle.CardsPlayed
            .Select((play, index) => new { Play = play, Index = index })
            .ToList();

        var ordered = GameCombatRules.OrderByEffectPriority(
            indexed,
            item =>
            {
                var card = _cardRegistry.GetById(item.Play.CardId);
                return card?.EffectKey ?? "pass";
            },
            item => item.Index)
            .Select(item => item.Play)
            .ToList();

        foreach (var play in ordered)
        {
            var actor = room.Session.GetPlayer(play.PlayerId);
            if (actor is null || !actor.IsAlive)
                continue;

            var card = _cardRegistry.GetById(play.CardId);
            if (card is null || IsPassCard(card.Id))
                continue;

            var hand = room.Session.PlayerHands.FirstOrDefault(h => h.UserId == play.PlayerId);
            if (hand is null)
                continue;

            if (play.InventorySlotIndex is int slotIndex)
            {
                if (hand.GetInventorySlot(slotIndex) != play.CardId)
                    continue;
            }
            else if (!hand.ContainsCard(play.CardId))
            {
                continue;
            }

            var targetId = play.TargetUserId
                ?? ResolveTargetId(battle, play.PlayerId, card, null);

            try
            {
                var effectResult = _rulesEngine.ApplyCardEffect(room.Session, play.PlayerId, card, targetId);
                if (play.InventorySlotIndex is int slot)
                    hand.SetInventorySlot(slot, null);
                else
                    _consumption.ConsumeAfterPlay(hand, card);

                var opponent = battle.PlayerA == play.PlayerId ? battle.PlayerB : battle.PlayerA;
                BotObservationRecorder.OnCardOutcome(
                    room.Session,
                    play.PlayerId,
                    targetId,
                    effectResult,
                    card.EffectKey,
                    new[] { battle.PlayerA, battle.PlayerB, opponent });
            }
            catch (ServiceException)
            {
                // Card may no longer be valid after earlier effects in the resolution chain.
            }
        }

        battle.Status = BattleAggregateStatus.Finished;
        await _battleStore.SaveAsync(battle, cancellationToken);
    }

    private async Task<Battle> QueuePassCardAsync(
        RoomState room,
        Battle battle,
        Guid userId,
        Guid cardId,
        int? inventorySlotIndex,
        CancellationToken cancellationToken)
    {
        var card = _cardRegistry.GetById(cardId)!;
        var hand = room.Session.PlayerHands.First(h => h.UserId == userId);
        ValidateQueuedPlay(room, userId, hand, card, userId);

        QueueCardPlay(battle, userId, cardId, inventorySlotIndex, userId);

        var actor = room.Session.GetPlayer(userId)!;
        var isFirstAction = GameCombatRules.IsFirstActionOfTurn(actor);
        ConsumeBattleAction(room, userId);
        if (isFirstAction)
            GameCombatRules.CompleteTurnAfterFirstPass(actor);

        room.Statistics.TotalCardsPlayed++;
        RecordPlayerAction(room, userId);
        MarkAsActive(room, userId);
        ApplyPublicAction(battle, userId, BattlePublicAction.Pass);

        await _battleStore.SaveAsync(battle, cancellationToken);
        return battle;
    }

    private void ValidateQueuedPlay(
        RoomState room,
        Guid userId,
        PlayerCardState hand,
        CardDefinition card,
        Guid targetUserId)
    {
        var actor = room.Session.GetPlayer(userId)
            ?? throw new ServiceException("Player not found.");

        if (!actor.IsAlive)
            throw new ServiceException("Dead players cannot take actions.");

        if (actor.RemainingActions <= 0)
            throw new ServiceException("No remaining actions this turn.");

        _cardValidator.ValidateCardPlayable(card.Id);
        _cardValidator.ValidateCardNotDisabled(hand, card.Id);
        _cardValidator.ValidateRoleCanPlayCard(actor.Role, card.EffectKey);
        _cardValidator.ValidateTargetRequired(card.EffectKey, targetUserId, userId);
    }

    private void QueueCardPlay(
        Battle battle,
        Guid userId,
        Guid cardId,
        int? inventorySlotIndex,
        Guid targetUserId)
    {
        battle.CardsPlayed.Add(new BattleCardPlay
        {
            PlayerId = userId,
            CardId = cardId,
            InventorySlotIndex = inventorySlotIndex,
            TargetUserId = targetUserId,
            PlayedAt = DateTime.UtcNow
        });
    }

    private static Guid ResolveTargetId(Battle battle, Guid userId, CardDefinition card, Guid? targetUserId)
    {
        var effect = card.EffectKey;
        if (effect.Equals("heal", StringComparison.OrdinalIgnoreCase) ||
            effect.Equals("shoot", StringComparison.OrdinalIgnoreCase) ||
            effect.Equals("infect", StringComparison.OrdinalIgnoreCase) ||
            effect.Equals("zombie_poison", StringComparison.OrdinalIgnoreCase) ||
            effect.Equals("power_zombie", StringComparison.OrdinalIgnoreCase))
        {
            var opponent = battle.PlayerA == userId ? battle.PlayerB : battle.PlayerA;
            var targetId = targetUserId ?? opponent;
            if (targetId == userId)
                throw new ServiceException("This card must target the battle opponent.");
            return targetId;
        }

        return targetUserId ?? userId;
    }

    private static void ResolveInventorySlot(
        PlayerCardState hand,
        Guid cardId,
        int? inventorySlotIndex,
        Battle battle,
        Guid userId)
    {
        if (inventorySlotIndex is int slot)
        {
            if (slot is < 0 or >= PlayerHandExtensions.InventorySlotCount)
                throw new ServiceException("Invalid inventory slot.");

            if (hand.GetInventorySlot(slot) != cardId)
                throw new ServiceException("Card not in that inventory slot.");

            if (battle.CardsPlayed.Any(c =>
                    c.PlayerId == userId &&
                    c.InventorySlotIndex == slot))
                throw new ServiceException("That inventory slot was already used this battle.");
            return;
        }

        if (!hand.ContainsCard(cardId))
            throw new ServiceException("Card not in hand.");

        if (battle.CardsPlayed.Any(c => c.PlayerId == userId && c.CardId == cardId && c.InventorySlotIndex is null))
            throw new ServiceException("That card was already queued this battle.");
    }

    private static void ConsumeBattleAction(RoomState room, Guid userId)
    {
        var actor = room.Session.GetPlayer(userId)
            ?? throw new ServiceException("Player not found.");

        if (!actor.IsAlive)
            throw new ServiceException("Dead players cannot take actions.");

        if (actor.RemainingActions <= 0)
            throw new ServiceException("No remaining actions this turn.");

        actor.ActionsUsedThisTurn++;
    }

    private static bool IsPassCard(Guid cardId) =>
        cardId == ActionCardCatalog.Pass;

    /// <summary>
    /// Auto-pass only after a disconnect that started during this battle.
    /// Lobby/reveal reconnects must not burn the turn before the player can act.
    /// </summary>
    private bool IsGraceExpired(RoomState room, Battle battle, Guid playerId, DateTime now)
    {
        var player = room.GetPlayer(playerId);
        if (player?.DisconnectedAt is not DateTime since)
            return false;

        var battleStart = battle.BattleEndsAt?.AddSeconds(-_settings.CardBattleSeconds);
        if (battleStart is DateTime start && since < start)
            return false;

        return now >= since.AddSeconds(_settings.BattleDisconnectGraceSeconds);
    }

    public Task SyncPairFromBattle(BattlePair pair, Battle battle)
    {
        pair.Player1Summary = battle.PlayerAPublicAction;
        pair.Player2Summary = battle.PlayerBPublicAction;
        pair.BattleSession.Player1Action = battle.PlayerAPublicAction;
        pair.BattleSession.Player2Action = battle.PlayerBPublicAction;
        pair.BattleSession.Player1Finished = battle.PlayerAFinished;
        pair.BattleSession.Player2Finished = battle.PlayerBFinished;
        pair.Player1PlayedCardIds = battle.CardsPlayed
            .Where(c => c.PlayerId == battle.PlayerA)
            .OrderBy(c => c.PlayedAt)
            .Select(c => c.CardId)
            .ToList();
        pair.Player2PlayedCardIds = battle.CardsPlayed
            .Where(c => c.PlayerId == battle.PlayerB)
            .OrderBy(c => c.PlayedAt)
            .Select(c => c.CardId)
            .ToList();
        pair.Status = battle.IsFinished
            || (pair.BattleSession.Player1Finished && pair.BattleSession.Player2Finished)
            ? BattlePairStatus.Finished
            : pair.Status;
        return Task.CompletedTask;
    }

    public BattlePair CreatePairReference(Battle battle) =>
        new()
        {
            PairId = battle.BattleId,
            Player1Id = battle.PlayerA,
            Player2Id = battle.PlayerB,
            Status = BattlePairStatus.Pending,
            BattleSession = new PairBattleSession { PairId = battle.BattleId }
        };

    private async Task<Battle> RequireBattleAsync(
        Guid matchId,
        Guid battleId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var battle = await _battleStore.GetAsync(matchId, battleId, cancellationToken)
            ?? throw new ServiceException("Battle not found.");

        if (!battle.IsMember(userId))
            throw new ServiceException("You are not in this battle.");

        if (battle.Status != BattleAggregateStatus.InProgress)
            throw new ServiceException("Battle is not active.");

        if (HasFinished(battle, userId))
            throw new ServiceException("Your turn in this battle is already over.");

        return battle;
    }

    private static bool HasFinished(Battle battle, Guid userId) =>
        battle.PlayerA == userId ? battle.PlayerAFinished : battle.PlayerBFinished;

    private static void ApplyPublicAction(Battle battle, Guid userId, BattlePublicAction action)
    {
        var current = battle.PlayerA == userId
            ? battle.PlayerAPublicAction
            : battle.PlayerB == userId
                ? battle.PlayerBPublicAction
                : BattlePublicAction.None;

        if (current == BattlePublicAction.Action && action == BattlePublicAction.Pass)
            return;

        if (battle.PlayerA == userId)
            battle.PlayerAPublicAction = action;
        else if (battle.PlayerB == userId)
            battle.PlayerBPublicAction = action;
    }

    private static void MarkAsActive(RoomState room, Guid userId) =>
        RoomActivityTracker.MarkActed(room, userId);

    private static void MarkPlayerFinished(Battle battle, Guid userId, BattlePublicAction action)
    {
        ApplyPublicAction(battle, userId, action);

        if (battle.PlayerA == userId)
            battle.PlayerAFinished = true;
        else if (battle.PlayerB == userId)
            battle.PlayerBFinished = true;

        if (battle.IsFinished)
            battle.Status = BattleAggregateStatus.Finished;
    }

    private static void RecordPlayerAction(RoomState room, Guid userId)
    {
        room.Statistics.ActionsByPlayer.TryGetValue(userId, out var count);
        room.Statistics.ActionsByPlayer[userId] = count + 1;
    }
}
