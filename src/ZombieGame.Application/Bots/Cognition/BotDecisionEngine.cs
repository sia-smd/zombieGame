namespace ZombieGame.Application.Bots.Cognition;

using ZombieGame.Application.Bots;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Domain.Cards;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Models;

public sealed class BotDecisionEngine
{
    private readonly ICardRegistry _cards;
    private readonly ICardPlayValidator _validator;

    public BotDecisionEngine(ICardRegistry cards, ICardPlayValidator validator)
    {
        _cards = cards;
        _validator = validator;
    }

    public bool ShouldPassBattle(GameSessionState state, Guid botUserId, Random random, Guid? battleOpponentId = null)
    {
        var bot = state.GetPlayer(botUserId);
        if (bot is null || !bot.IsAlive)
            return true;

        if (TryFindCardPlay(state, botUserId, random, battleOpponentId) is null)
            return true;

        var personality = GetPersonality(state, botUserId);
        var passChance = 0.25;
        passChance += (100 - personality.Aggression) / 300.0;
        passChance += personality.Patience / 500.0;
        passChance -= StalemateAggressionBoost(state);

        if (bot.Role == PlayerRole.Human && battleOpponentId is Guid opponent)
            passChance *= SuspicionPassMultiplier(state, botUserId, opponent, personality);

        if (bot.IsInfectedTeam)
            passChance *= InfectedTeamPassMultiplier(state, botUserId, bot.Role, personality, battleOpponentId);

        passChance = Math.Clamp(passChance, 0.05, 0.65);

        return random.NextDouble() < passChance;
    }

    public BotCardPlayDecision? TryFindCardPlay(
        GameSessionState state,
        Guid botUserId,
        Random random,
        Guid? battleOpponentId = null)
    {
        var bot = state.GetPlayer(botUserId);
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == botUserId);
        if (bot is null || hand is null)
            return null;

        var personality = GetPersonality(state, botUserId);

        var patienceSkipChance = personality.Patience / 300.0;
        if (state.DaysSinceLastElimination < 10)
            patienceSkipChance *= 0.6;
        if (bot.IsInfectedTeam)
            patienceSkipChance *= 0.35;

        if (personality.Patience > 75 && random.NextDouble() < patienceSkipChance)
            return null;

        if (bot.Role == PlayerRole.Human)
        {
            var suspicionPlay = TrySuspicionBasedHumanPlay(
                state, botUserId, hand, personality, random, battleOpponentId);
            if (suspicionPlay is not null)
                return suspicionPlay;
        }

        if (bot.IsInfectedTeam)
        {
            var attackPlay = TryInfectedTeamAttackPlay(
                state, botUserId, hand, bot.Role, personality, random, battleOpponentId);
            if (attackPlay is not null)
                return attackPlay;
        }

        var cards = hand.EnumerateAllCards().OrderBy(_ => random.Next()).ToList();

        foreach (var cardId in cards)
        {
            var card = _cards.GetById(cardId);
            if (card is null)
                continue;

            try
            {
                _validator.ValidateRoleCanPlayCard(bot.Role, card.EffectKey);
                _validator.ValidateCardNotDisabled(hand, cardId);
            }
            catch
            {
                continue;
            }

            if (card.EffectKey is "shoot" or "heal")
                continue;

            var targetId = battleOpponentId is Guid opponent
                ? PickBattleTarget(state, botUserId, card.EffectKey, opponent, random, personality)
                : PickTarget(state, botUserId, card.EffectKey, random, personality);

            if (targetId is not Guid resolvedTarget)
                continue;

            if (!MeetsRiskThreshold(state, botUserId, card.EffectKey, resolvedTarget, personality, random))
                continue;

            return new BotCardPlayDecision(cardId, resolvedTarget);
        }

        return null;
    }

    public Guid DecideVoteTarget(GameSessionState state, Guid botUserId, Random random)
    {
        var candidates = state.AlivePlayers.Where(p => p.UserId != botUserId).ToList();
        if (candidates.Count == 0)
            return botUserId;

        var personality = GetPersonality(state, botUserId);
        var weights = new List<(Guid Id, double Weight)>();

        foreach (var candidate in candidates)
        {
            var publicSuspicion = SuspicionScoring.GetSuspicion(state, botUserId, candidate.UserId);
            var memorySuspicion = BotMemoryService.GetMemorySuspicion(state, botUserId, candidate.UserId);
            var belief = BotBeliefService.GetBelief(state, botUserId, candidate.UserId);
            var trustDamping = 1.0 - personality.Trust / 180.0;
            var weight = Math.Max(0.01,
                (publicSuspicion + memorySuspicion + belief / 10.0) * trustDamping);

            if (IsPubliclyKnownInfected(state, botUserId, candidate.UserId))
                weight += 3.0 * (personality.Confidence / 100.0);

            weight += personality.Aggression / 500.0;
            weights.Add((candidate.UserId, weight));
        }

        var sorted = weights.OrderByDescending(w => w.Weight).ToList();
        if (sorted.Count == 0)
            return botUserId;

        if (personality.Confidence < 35 && random.NextDouble() < 0.2)
            return sorted[random.Next(Math.Min(3, sorted.Count))].Id;

        if (sorted.Count >= 2 &&
            personality.Confidence >= 55 &&
            sorted[0].Weight >= sorted[1].Weight * 1.4 &&
            random.NextDouble() < 0.55 + personality.Confidence / 250.0)
            return sorted[0].Id;

        return WeightedVotePick(sorted, random, personality);
    }

    public static double GetInfectPlayProbability(BotPersonality personality, PlayerRole role)
    {
        var baseProb = role switch
        {
            PlayerRole.PowerZombie => 0.78,
            PlayerRole.Zombie => 0.58,
            _ => 0.50
        };

        return Math.Clamp(
            baseProb
            + personality.Aggression / 220.0
            + personality.RiskTolerance / 350.0
            - personality.Patience / 450.0,
            0.30,
            0.95);
    }

    private BotCardPlayDecision? TryInfectedTeamAttackPlay(
        GameSessionState state,
        Guid botUserId,
        PlayerCardState hand,
        PlayerRole botRole,
        BotPersonality personality,
        Random random,
        Guid? battleOpponentId)
    {
        if (!GameCombatRules.CanInfectedRoleAttackToday(botRole, state.CurrentDayEvent))
            return null;

        const string effectKey = "zombie_poison";
        var cardId = FindPlayableCard(state, botUserId, hand, effectKey);
        if (cardId is not Guid resolvedCardId)
            return null;

        Guid? targetId = battleOpponentId is Guid opponent
            ? PickBattleTarget(state, botUserId, effectKey, opponent, random, personality)
            : PickTarget(state, botUserId, effectKey, random, personality);

        if (targetId is not Guid resolvedTarget)
            return null;

        if (IsPubliclyKnownInfected(state, botUserId, resolvedTarget))
            return null;

        var attackProb = GetInfectPlayProbability(personality, botRole);
        if (random.NextDouble() >= attackProb)
            return null;

        return new BotCardPlayDecision(resolvedCardId, resolvedTarget);
    }

    private static bool CanInfectedAttackToday(PlayerRole role, DayEventType dayEvent) =>
        GameCombatRules.CanInfectedRoleAttackToday(role, dayEvent);

    private double InfectedTeamPassMultiplier(
        GameSessionState state,
        Guid botUserId,
        PlayerRole role,
        BotPersonality personality,
        Guid? battleOpponentId)
    {
        if (!GameCombatRules.CanInfectedRoleAttackToday(role, state.CurrentDayEvent))
            return 1.0;

        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == botUserId);
        if (hand is null)
            return 1.0;

        const string effectKey = "zombie_poison";
        if (!HasPlayableCard(hand, botUserId, state, effectKey))
            return 1.0;

        if (battleOpponentId is Guid opponent &&
            IsPubliclyKnownInfected(state, botUserId, opponent))
            return 1.0;

        var attackProb = GetInfectPlayProbability(personality, role);
        if (attackProb >= 0.80)
            return 0.22;
        if (attackProb >= 0.65)
            return 0.38;
        if (attackProb >= 0.50)
            return 0.55;

        return 0.75;
    }

    public static double GetHealPlayProbability(int belief, BotPersonality personality)
    {
        var baseProb = belief switch
        {
            >= 75 => 0.95,
            >= 60 => 0.88,
            >= 50 => 0.55,
            >= 40 => 0.28,
            _ => 0.10
        };

        return Math.Clamp(
            baseProb + personality.RiskTolerance / 500.0 - personality.Patience / 800.0,
            0.05,
            0.98);
    }

    public static double GetShootPlayProbability(int belief, BotPersonality personality)
    {
        var baseProb = belief switch
        {
            >= 90 => 0.90,
            >= 80 => 0.75,
            >= 70 => 0.42,
            >= 60 => 0.18,
            _ => 0.05
        };

        return Math.Clamp(
            baseProb + personality.Aggression / 400.0 + personality.RiskTolerance / 600.0 - personality.Patience / 500.0,
            0.03,
            0.95);
    }

    private BotCardPlayDecision? TrySuspicionBasedHumanPlay(
        GameSessionState state,
        Guid botUserId,
        PlayerCardState hand,
        BotPersonality personality,
        Random random,
        Guid? battleOpponentId)
    {
        var healCardId = FindPlayableCard(state, botUserId, hand, "heal");
        var shootCardId = FindPlayableCard(state, botUserId, hand, "shoot");
        if (healCardId is null && shootCardId is null)
            return null;

        var candidates = battleOpponentId is Guid opponent
            ? state.AlivePlayers.Where(p => p.UserId == opponent).ToList()
            : state.AlivePlayers.Where(p => p.UserId != botUserId).ToList();

        var suspectId = PickHighestBeliefSuspect(state, botUserId, candidates);
        if (suspectId is not Guid targetId)
            return null;

        var belief = BotBeliefService.GetBelief(state, botUserId, targetId);

        if (healCardId is null && shootCardId is null)
            return null;

        var healProb = healCardId is not null
            ? GetHealPlayProbability(belief, personality)
            : 0.0;
        var shootProb = shootCardId is not null
            ? GetShootPlayProbability(belief, personality)
            : 0.0;

        if (IsPubliclyKnownInfected(state, botUserId, targetId) &&
            GameCombatRules.CanHealTarget(state.GetPlayer(targetId)!))
            healProb = Math.Max(healProb, 0.92);
        if (IsPubliclyRevealedThreat(state, botUserId, targetId))
            shootProb = Math.Max(shootProb, 0.85);

        var actProb = Math.Max(healProb, shootProb);
        if (random.NextDouble() >= actProb)
            return null;

        if (healCardId is Guid healId && shootCardId is Guid shootId)
        {
            var canHealTarget = GameCombatRules.CanHealTarget(state.GetPlayer(targetId)!);
            if (canHealTarget)
            {
                var healWeight = healProb;
                var shootWeight = shootProb;
                var pickHeal = random.NextDouble() * (healWeight + shootWeight) < healWeight;
                return pickHeal
                    ? new BotCardPlayDecision(healId, targetId)
                    : new BotCardPlayDecision(shootId, targetId);
            }

            return new BotCardPlayDecision(shootId, targetId);
        }

        if (healCardId is Guid onlyHealId)
        {
            if (GameCombatRules.CanHealTarget(state.GetPlayer(targetId)!))
                return new BotCardPlayDecision(onlyHealId, targetId);
            return null;
        }

        if (shootCardId is Guid onlyShootId)
            return new BotCardPlayDecision(onlyShootId, targetId);

        return null;
    }

    private Guid? PickHighestBeliefSuspect(
        GameSessionState state,
        Guid botUserId,
        List<GamePlayerState> candidates)
    {
        if (candidates.Count == 0)
            return null;

        var ranked = candidates
            .OrderByDescending(p => BotBeliefService.GetBelief(state, botUserId, p.UserId))
            .ThenByDescending(p => BotMemoryService.GetMemorySuspicion(state, botUserId, p.UserId))
            .ToList();

        var top = ranked[0];
        var belief = BotBeliefService.GetBelief(state, botUserId, top.UserId);
        if (belief >= 35 || IsPubliclyKnownInfected(state, botUserId, top.UserId))
            return top.UserId;

        return null;
    }

    private double SuspicionPassMultiplier(
        GameSessionState state,
        Guid botUserId,
        Guid opponentId,
        BotPersonality personality)
    {
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == botUserId);
        if (hand is null)
            return 1.0;

        var belief = BotBeliefService.GetBelief(state, botUserId, opponentId);
        var hasHeal = HasPlayableCard(hand, botUserId, state, "heal");
        var hasShoot = HasPlayableCard(hand, botUserId, state, "shoot");

        if (!hasHeal && !hasShoot)
            return 1.0;

        var healLikely = hasHeal && GetHealPlayProbability(belief, personality) >= 0.55;
        var shootLikely = hasShoot && GetShootPlayProbability(belief, personality) >= 0.45;

        if (healLikely || shootLikely)
            return 0.30;

        if (belief >= 50)
            return 0.55;

        return 1.0;
    }

    private bool HasPlayableCard(PlayerCardState hand, Guid botUserId, GameSessionState state, string effectKey)
    {
        var bot = state.GetPlayer(botUserId);
        if (bot is null)
            return false;

        foreach (var cardId in hand.EnumerateAllCards())
        {
            var card = _cards.GetById(cardId);
            if (card is null || !card.EffectKey.Equals(effectKey, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                _validator.ValidateRoleCanPlayCard(bot.Role, card.EffectKey);
                _validator.ValidateCardNotDisabled(hand, cardId);
                return true;
            }
            catch
            {
                continue;
            }
        }

        return false;
    }

    private Guid? FindPlayableCard(
        GameSessionState state,
        Guid botUserId,
        PlayerCardState hand,
        string effectKey)
    {
        var bot = state.GetPlayer(botUserId);
        if (bot is null)
            return null;

        foreach (var cardId in hand.EnumerateAllCards())
        {
            var card = _cards.GetById(cardId);
            if (card is null || !card.EffectKey.Equals(effectKey, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                _validator.ValidateRoleCanPlayCard(bot.Role, card.EffectKey);
                _validator.ValidateCardNotDisabled(hand, cardId);
                return cardId;
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    private Guid? PickBattleTarget(
        GameSessionState state,
        Guid botUserId,
        string effectKey,
        Guid opponentId,
        Random random,
        BotPersonality personality)
    {
        if (effectKey.Equals("shield", StringComparison.OrdinalIgnoreCase))
            return botUserId;

        var opponent = state.GetPlayer(opponentId);
        if (opponent is null || !opponent.IsAlive || opponentId == botUserId)
            return null;

        return effectKey switch
        {
            "infect" or "power_zombie" or "zombie_poison" => CanTargetForInfection(state, botUserId, opponentId) ? opponentId : null,
            _ => opponentId
        };
    }

    private Guid? PickTarget(
        GameSessionState state,
        Guid botUserId,
        string effectKey,
        Random random,
        BotPersonality personality)
    {
        if (effectKey.Equals("shield", StringComparison.OrdinalIgnoreCase))
            return botUserId;

        var candidates = state.AlivePlayers.Where(p => p.UserId != botUserId).ToList();
        if (candidates.Count == 0)
            return null;

        return effectKey switch
        {
            "infect" or "power_zombie" or "zombie_poison" => PickInfectionTarget(state, botUserId, candidates, random, personality),
            _ => candidates[random.Next(candidates.Count)].UserId
        };
    }

    private Guid? PickInfectionTarget(
        GameSessionState state,
        Guid botUserId,
        List<GamePlayerState> candidates,
        Random random,
        BotPersonality personality)
    {
        var unknown = candidates
            .Where(p => !IsPubliclyKnownInfected(state, botUserId, p.UserId))
            .Where(p => !IsPubliclyRevealedThreat(state, botUserId, p.UserId))
            .ToList();

        if (unknown.Count == 0)
            unknown = candidates.Where(p => !IsPubliclyKnownInfected(state, botUserId, p.UserId)).ToList();

        if (unknown.Count == 0)
            return null;

        if (personality.Aggression > 55)
            return unknown.OrderByDescending(p => CombinedScore(state, botUserId, p.UserId)).First().UserId;

        return unknown[random.Next(unknown.Count)].UserId;
    }

    private bool MeetsRiskThreshold(
        GameSessionState state,
        Guid botUserId,
        string effectKey,
        Guid targetId,
        BotPersonality personality,
        Random random)
    {
        if (!RequiresRisk(effectKey))
            return true;

        var actor = state.GetPlayer(botUserId);
        if (actor?.IsInfectedTeam == true &&
            effectKey is "infect" or "power_zombie" or "zombie_poison" &&
            !IsPubliclyKnownInfected(state, botUserId, targetId))
            return true;

        if (IsPubliclyKnownInfected(state, botUserId, targetId) ||
            IsPubliclyRevealedThreat(state, botUserId, targetId))
            return true;

        var belief = BotBeliefService.GetBelief(state, botUserId, targetId);

        if (effectKey is "infect" or "power_zombie" or "zombie_poison")
            return belief >= 30 || (personality.RiskTolerance > 70 && random.NextDouble() < personality.RiskTolerance / 150.0);

        return personality.RiskTolerance > 70 && random.NextDouble() < personality.RiskTolerance / 150.0;
    }

    private static double StalemateAggressionBoost(GameSessionState state) => state.DaysSinceLastElimination switch
    {
        >= 10 => 0.25,
        >= 5 => 0.12,
        _ => 0
    };

    private static bool CanTargetForInfection(GameSessionState state, Guid botUserId, Guid targetId) =>
        !IsPubliclyKnownInfected(state, botUserId, targetId);

    private static bool IsPubliclyRevealedThreat(GameSessionState state, Guid observerId, Guid targetId)
    {
        if (IsPubliclyKnownInfected(state, observerId, targetId))
            return true;

        var target = state.GetPlayer(targetId);
        return target?.HasRevealedThisDay == true &&
               state.BotCognition.TryGetValue(observerId, out var cognition) &&
               cognition.ObservedInfectors.Contains(targetId);
    }

    private static bool IsPubliclyKnownInfected(GameSessionState state, Guid observerId, Guid targetId)
    {
        if (state.BotCognition.TryGetValue(observerId, out var cognition) &&
            cognition.PubliclyKnownInfected.Contains(targetId))
            return true;

        return false;
    }

    private static double CombinedScore(GameSessionState state, Guid observerId, Guid targetId) =>
        SuspicionScoring.GetSuspicion(state, observerId, targetId) +
        BotMemoryService.GetMemorySuspicion(state, observerId, targetId) +
        BotBeliefService.GetBelief(state, observerId, targetId) / 10.0;

    private static bool RequiresRisk(string effectKey) =>
        effectKey is "infect" or "power_zombie" or "zombie_poison";

    private static BotPersonality GetPersonality(GameSessionState state, Guid botUserId)
    {
        if (state.BotCognition.TryGetValue(botUserId, out var cognition))
            return cognition.Personality;

        return new BotPersonality
        {
            Aggression = 50,
            RiskTolerance = 50,
            Trust = 50,
            Patience = 50,
            Confidence = 50
        };
    }

    private static Guid WeightedVotePick(
        List<(Guid Id, double Weight)> sorted,
        Random random,
        BotPersonality personality)
    {
        var topCount = personality.Confidence >= 60 ? 2 : 3;
        var focus = sorted.Take(Math.Min(topCount, sorted.Count)).ToList();
        var total = focus.Sum(o => o.Weight);
        var roll = random.NextDouble() * total;

        foreach (var option in focus)
        {
            roll -= option.Weight;
            if (roll <= 0)
                return option.Id;
        }

        return focus[0].Id;
    }
}
