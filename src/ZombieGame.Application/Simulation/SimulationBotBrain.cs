namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.Bots;
using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public sealed class SimulationBotBrain
{
    private readonly BotDecisionEngine _decisions;
    private readonly ICardRegistry _cards;

    public SimulationBotBrain(ICardRegistry cardRegistry, ICardPlayValidator cardValidator)
    {
        _cards = cardRegistry;
        _decisions = new BotDecisionEngine(cardRegistry, cardValidator);
    }

    public BotCardPlay? TryFindCardPlay(
        GameSessionState state,
        GamePlayerState bot,
        Random random,
        Guid? battleOpponentId = null)
    {
        var play = _decisions.TryFindCardPlay(state, bot.UserId, random, battleOpponentId);
        if (play is null)
            return null;

        var effectKey = _cards.GetById(play.CardId)?.EffectKey ?? "unknown";
        return new BotCardPlay(play.CardId, effectKey, play.TargetUserId ?? bot.UserId);
    }

    public bool ShouldPassBattle(GameSessionState state, GamePlayerState bot, Random random, Guid opponentId) =>
        _decisions.ShouldPassBattle(state, bot.UserId, random, opponentId);

    public BotTurnDecision DecideDayAction(GameSessionState state, GamePlayerState bot, Random random, int maxPlays)
    {
        var plays = new List<BotCardPlay>();
        if (maxPlays <= 0)
            return new BotTurnDecision(false, plays);

        if (_decisions.ShouldPassBattle(state, bot.UserId, random))
            return new BotTurnDecision(true, plays);

        while (plays.Count < maxPlays)
        {
            var play = _decisions.TryFindCardPlay(state, bot.UserId, random);
            if (play is null)
                break;

            plays.Add(new BotCardPlay(play.CardId, _cards.GetById(play.CardId)?.EffectKey ?? "card", play.TargetUserId ?? bot.UserId));
        }

        return new BotTurnDecision(plays.Count == 0, plays);
    }

    public Guid DecideVoteTarget(GameSessionState state, GamePlayerState bot, Random random, bool useSuspicionBasedVoting = true)
    {
        if (!useSuspicionBasedVoting)
            return PickRandomVoteTarget(state, bot, random);

        return _decisions.DecideVoteTarget(state, bot.UserId, random);
    }

    private static Guid PickRandomVoteTarget(GameSessionState state, GamePlayerState bot, Random random)
    {
        var candidates = state.AlivePlayers.Where(p => p.UserId != bot.UserId).ToList();
        return candidates.Count == 0 ? bot.UserId : candidates[random.Next(candidates.Count)].UserId;
    }
}

public sealed record BotCardPlay(Guid CardId, string EffectKey, Guid TargetUserId);

public sealed record BotTurnDecision(bool EndDayImmediately, IReadOnlyList<BotCardPlay> Plays);
