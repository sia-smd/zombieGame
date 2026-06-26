namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.Bots;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public sealed class SimulationBotBrain
{
    private readonly ICardRegistry _cardRegistry;
    private readonly ICardPlayValidator _cardValidator;

    public SimulationBotBrain(ICardRegistry cardRegistry, ICardPlayValidator cardValidator)
    {
        _cardRegistry = cardRegistry;
        _cardValidator = cardValidator;
    }

    public BotCardPlay? TryFindCardPlay(GameSessionState state, GamePlayerState bot, Random random)
    {
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == bot.UserId);
        if (hand is null || hand.CardIds.Count == 0)
            return null;

        foreach (var cardId in hand.CardIds.OrderBy(_ => random.Next()))
        {
            var card = _cardRegistry.GetById(cardId);
            if (card is null) continue;

            try
            {
                _cardValidator.ValidateRoleCanPlayCard(bot.Role, card.EffectKey);
                _cardValidator.ValidateCardNotDisabled(hand, cardId);
            }
            catch
            {
                continue;
            }

            var targetId = PickTarget(state, bot, card.EffectKey, random);
            if (targetId is null) continue;

            return new BotCardPlay(cardId, card.EffectKey, targetId.Value);
        }

        return null;
    }

    public BotTurnDecision DecideDayAction(GameSessionState state, GamePlayerState bot, Random random, int maxPlays)
    {
        var plays = new List<BotCardPlay>();
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == bot.UserId);
        if (hand is null || hand.CardIds.Count == 0 || maxPlays <= 0)
            return new BotTurnDecision(false, plays);

        if (random.NextDouble() < 0.25)
            return new BotTurnDecision(true, plays);

        var shuffled = hand.CardIds.OrderBy(_ => random.Next()).ToList();
        foreach (var cardId in shuffled)
        {
            if (plays.Count >= maxPlays) break;

            var card = _cardRegistry.GetById(cardId);
            if (card is null) continue;

            try
            {
                _cardValidator.ValidateRoleCanPlayCard(bot.Role, card.EffectKey);
                _cardValidator.ValidateCardNotDisabled(hand, cardId);
            }
            catch
            {
                continue;
            }

            var targetId = PickTarget(state, bot, card.EffectKey, random);
            if (targetId is null) continue;

            plays.Add(new BotCardPlay(cardId, card.EffectKey, targetId.Value));
        }

        return new BotTurnDecision(plays.Count == 0, plays);
    }

    public Guid DecideVoteTarget(GameSessionState state, GamePlayerState bot, Random random, bool useSuspicionBasedVoting = true)
    {
        if (useSuspicionBasedVoting)
            return SuspicionScoring.PickHighestSuspicionTarget(state, bot.UserId);

        return DecideRandomVoteTarget(state, bot, random);
    }

    private static Guid DecideRandomVoteTarget(GameSessionState state, GamePlayerState bot, Random random)
    {
        var candidates = state.AlivePlayers.Where(p => p.UserId != bot.UserId).ToList();
        if (candidates.Count == 0)
            return bot.UserId;

        var humans = candidates.Where(p => p.Role == PlayerRole.Human).ToList();
        if (bot.IsInfectedTeam && humans.Count > 0)
            return humans[random.Next(humans.Count)].UserId;

        var infected = candidates.Where(p => p.IsInfectedTeam).ToList();
        if (bot.Role == PlayerRole.Human && infected.Count > 0)
            return infected[random.Next(infected.Count)].UserId;

        return candidates[random.Next(candidates.Count)].UserId;
    }

    private static Guid? PickTarget(GameSessionState state, GamePlayerState bot, string effectKey, Random random)
    {
        if (effectKey.Equals("shield", StringComparison.OrdinalIgnoreCase))
            return bot.UserId;

        var alive = state.AlivePlayers.Where(p => p.UserId != bot.UserId).ToList();
        if (alive.Count == 0) return null;

        return effectKey switch
        {
            "infect" or "power_zombie" => alive.FirstOrDefault(p => p.Role == PlayerRole.Human)?.UserId
                ?? alive[random.Next(alive.Count)].UserId,
            "shoot" => alive.FirstOrDefault(p => p.IsInfectedTeam && p.HasRevealedThisDay)?.UserId,
            "heal" => alive.FirstOrDefault(p => p.Role == PlayerRole.Zombie && p.HasRevealedThisDay)?.UserId,
            _ => alive[random.Next(alive.Count)].UserId
        };
    }
}

public sealed record BotCardPlay(Guid CardId, string EffectKey, Guid TargetUserId);

public sealed record BotTurnDecision(bool EndDayImmediately, IReadOnlyList<BotCardPlay> Plays);
