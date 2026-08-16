namespace ZombieGame.Application.Room.Battle;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public interface IBattleService
{
    Task<Battle> CreateBattleAsync(RoomState room, Guid playerA, Guid playerB, TimeSpan duration, CancellationToken cancellationToken = default);
    Task StartBattlesForDayAsync(RoomState room, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<Battle> PlayCardAsync(RoomState room, Guid battleId, Guid userId, Guid cardId, Guid? targetUserId, CancellationToken cancellationToken = default);
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
    private readonly RoomSettings _settings;
    private readonly GameSettings _gameSettings;

    public BattleService(
        IBattleStore battleStore,
        IGameRulesEngine rulesEngine,
        ICardRegistry cardRegistry,
        ICardConsumptionService consumption,
        IOptions<RoomSettings> settings,
        IOptions<GameSettings> gameSettings)
    {
        _battleStore = battleStore;
        _rulesEngine = rulesEngine;
        _cardRegistry = cardRegistry;
        _consumption = consumption;
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
            // Fresh day turn: never inherit finished/pass flags from a reused aggregate.
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
        CancellationToken cancellationToken = default)
    {
        var battle = await RequireBattleAsync(room.MatchId, battleId, userId, cancellationToken);
        var card = _cardRegistry.GetById(cardId)
            ?? throw new ServiceException("Invalid card.");

        var hand = room.Session.PlayerHands.FirstOrDefault(h => h.UserId == userId)
            ?? throw new ServiceException("Player hand not found.");

        if (!hand.ContainsCard(cardId))
            throw new ServiceException("Card not in hand.");

        // Pass is an inventory card — same turn-ending rules as the old Pass button.
        if (card.EffectKey.Equals("pass", StringComparison.OrdinalIgnoreCase))
            return await PlayPassCardAsync(room, battle, userId, cardId, cancellationToken);

        // Heal / infect / poison / shotgun hit the battle opponent. Self-cards default to actor.
        Guid targetId;
        var effect = card.EffectKey;
        if (effect.Equals("heal", StringComparison.OrdinalIgnoreCase) ||
            effect.Equals("shoot", StringComparison.OrdinalIgnoreCase) ||
            effect.Equals("infect", StringComparison.OrdinalIgnoreCase) ||
            effect.Equals("zombie_poison", StringComparison.OrdinalIgnoreCase) ||
            effect.Equals("power_zombie", StringComparison.OrdinalIgnoreCase))
        {
            var opponent = battle.PlayerA == userId ? battle.PlayerB : battle.PlayerA;
            targetId = targetUserId ?? opponent;
            if (targetId == userId)
                throw new ServiceException("This card must target the battle opponent.");
        }
        else
        {
            targetId = targetUserId ?? userId;
        }

        var effectResult = _rulesEngine.PlayCard(room.Session, userId, card, targetId);

        _consumption.ConsumeAfterPlay(hand, card);

        var witnesses = new[] { battle.PlayerA, battle.PlayerB };
        BotObservationRecorder.OnCardOutcome(
            room.Session, userId, targetId, effectResult, card.EffectKey, witnesses);

        battle.CardsPlayed.Add(new BattleCardPlay
        {
            PlayerId = userId,
            CardId = cardId,
            TargetUserId = targetUserId,
            PlayedAt = DateTime.UtcNow
        });

        room.Statistics.TotalCardsPlayed++;
        RecordPlayerAction(room, userId);
        MarkAsActive(room, userId);

        // A turn is two actions: only finish once the action points are spent (or the player died).
        var actor = room.Session.GetPlayer(userId);
        if (actor is null || !actor.IsAlive || actor.RemainingActions <= 0)
            MarkPlayerFinished(battle, userId, BattlePublicAction.Action);
        else
            ApplyPublicAction(battle, userId, BattlePublicAction.Action);

        await _battleStore.SaveAsync(battle, cancellationToken);
        return battle;
    }

    private async Task<Battle> PlayPassCardAsync(
        RoomState room,
        Battle battle,
        Guid userId,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        BotObservationRecorder.OnPass(room.Session, userId, new[] { battle.PlayerA, battle.PlayerB });

        var actor = room.Session.GetPlayer(userId);
        if (actor is not null && actor.IsAlive && actor.RemainingActions > 0)
            _rulesEngine.PassAction(room.Session, userId);

        battle.CardsPlayed.Add(new BattleCardPlay
        {
            PlayerId = userId,
            CardId = cardId,
            TargetUserId = userId,
            PlayedAt = DateTime.UtcNow
        });

        room.Statistics.TotalCardsPlayed++;
        RecordPlayerAction(room, userId);
        MarkAsActive(room, userId);
        MarkPlayerFinished(battle, userId, BattlePublicAction.Pass);
        await _battleStore.SaveAsync(battle, cancellationToken);
        return battle;
    }

    public async Task<Battle> PassAsync(
        RoomState room,
        Guid battleId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var battle = await RequireBattleAsync(room.MatchId, battleId, userId, cancellationToken);
        BotObservationRecorder.OnPass(room.Session, userId, new[] { battle.PlayerA, battle.PlayerB });

        // Passing spends the whole turn, so the remaining action point is burned too.
        var actor = room.Session.GetPlayer(userId);
        if (actor is not null && actor.IsAlive && actor.RemainingActions > 0)
            _rulesEngine.PassAction(room.Session, userId);

        RecordPlayerAction(room, userId);
        MarkAsActive(room, userId);
        MarkPlayerFinished(battle, userId, BattlePublicAction.Pass);
        await _battleStore.SaveAsync(battle, cancellationToken);
        return battle;
    }

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

                AutoPass(room, battle, playerId);
                changed = true;
            }

            if (expired)
                battle.Status = BattleAggregateStatus.Finished;
            else if (!changed)
                continue;

            await _battleStore.SaveAsync(battle, cancellationToken);
            await SyncPairFromBattle(pair, battle);
        }
    }

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

    private static void AutoPass(RoomState room, Battle battle, Guid playerId)
    {
        var actor = room.Session.GetPlayer(playerId);
        if (actor is not null && actor.IsAlive)
            GameCombatRules.CompleteTurnAfterFirstPass(actor);

        MarkPlayerFinished(battle, playerId, BattlePublicAction.Pass);
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

        // Action+Pass in the same turn is still a public Action.
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
