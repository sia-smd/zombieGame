namespace ZombieGame.Application.GameRules;

using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.Common;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using Microsoft.Extensions.Options;

public interface IGameRulesEngine
{
    void StartMatch(GameSessionState state, Match match);
    CardEffectResult PlayCard(GameSessionState state, Guid actorUserId, CardDefinition card, Guid targetUserId);
    /// <summary>Apply a queued battle card without consuming an action point.</summary>
    CardEffectResult ApplyCardEffect(GameSessionState state, Guid actorUserId, CardDefinition card, Guid targetUserId);
    void PassAction(GameSessionState state, Guid actorUserId);
    void EndDayPhase(GameSessionState state, GameSettings settings);
    void CastVote(GameSessionState state, Guid voterId, Guid targetId);
    WinTeam? EvaluateImmediateWin(GameSessionState state);
    Task CompleteMatchIfWonAsync(GameSessionState state, Match match, CancellationToken cancellationToken = default);
    PhaseAdvanceResult AdvancePhase(GameSessionState state, Match match, GameSettings settings);
    bool TryAdvanceVotingToResolution(GameSessionState state, GameSettings settings);
    Task<WinTeam?> ProcessResolutionAsync(GameSessionState state, Match match, CancellationToken cancellationToken = default);
}

public record PhaseAdvanceResult(bool Advanced, string Message, GamePhase? NewPhase = null);

public sealed class GameRulesEngine : IGameRulesEngine
{
    private readonly IRoleAssignmentService _roles;
    private readonly ICardDealingService _dealing;
    private readonly IDayEventService _dayEvents;
    private readonly ICardEffectResolver _cardEffects;
    private readonly ICardPlayValidator _cardValidator;
    private readonly IVotingService _voting;
    private readonly IWinConditionService _winConditions;
    private readonly IMatchCompletionService _matchCompletion;
    private readonly GameSettings _settings;

    public GameRulesEngine(
        IRoleAssignmentService roles,
        ICardDealingService dealing,
        IDayEventService dayEvents,
        ICardEffectResolver cardEffects,
        ICardPlayValidator cardValidator,
        IVotingService voting,
        IWinConditionService winConditions,
        IMatchCompletionService matchCompletion,
        IOptions<GameSettings> settings)
    {
        _roles = roles;
        _dealing = dealing;
        _dayEvents = dayEvents;
        _cardEffects = cardEffects;
        _cardValidator = cardValidator;
        _voting = voting;
        _winConditions = winConditions;
        _matchCompletion = matchCompletion;
        _settings = settings.Value;
    }

    public void StartMatch(GameSessionState state, Match match)
    {
        state.CurrentPhase = GamePhase.Day;
        state.TurnNumber = 1;
        state.Votes.Clear();
        state.FriendlyFireCount = 0;
        state.WinTeam = WinTeam.None;

        _roles.AssignRoles(state, match.Players.Count);
        _dealing.InitializeHands(state);

        state.CurrentDayEvent = DayEventType.NormalDay;
        state.Metadata["dayEvent"] = state.CurrentDayEvent.ToString();

        ResetDailyCombatState(state);
        ResetActionPoints(state);
    }

    public CardEffectResult PlayCard(GameSessionState state, Guid actorUserId, CardDefinition card, Guid targetUserId)
    {
        ConsumeActionPoint(state, actorUserId);

        var actor = state.GetPlayer(actorUserId)!;
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == actorUserId)
            ?? throw new ServiceException("Player hand not found.");

        _cardValidator.ValidateCardPlayable(card.Id);
        _cardValidator.ValidateCardNotDisabled(hand, card.Id);
        _cardValidator.ValidateRoleCanPlayCard(actor.Role, card.EffectKey);
        _cardValidator.ValidateTargetRequired(card.EffectKey, targetUserId, actorUserId);

        var context = new CardEffectContext
        {
            State = state,
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            Card = card
        };

        var result = _cardEffects.Play(context);

        if (result.FriendlyFire)
            state.FriendlyFireCount++;

        RecordProgressCounters(state, actor, result);

        return result;
    }

    public CardEffectResult ApplyCardEffect(
        GameSessionState state,
        Guid actorUserId,
        CardDefinition card,
        Guid targetUserId)
    {
        var actor = state.GetPlayer(actorUserId)
            ?? throw new ServiceException("Player not found.");
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == actorUserId)
            ?? throw new ServiceException("Player hand not found.");

        _cardValidator.ValidateCardPlayable(card.Id);
        _cardValidator.ValidateCardNotDisabled(hand, card.Id);
        _cardValidator.ValidateRoleCanPlayCard(actor.Role, card.EffectKey);
        _cardValidator.ValidateTargetRequired(card.EffectKey, targetUserId, actorUserId);

        var context = new CardEffectContext
        {
            State = state,
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            Card = card
        };

        var result = _cardEffects.Play(context);

        if (result.FriendlyFire)
            state.FriendlyFireCount++;

        RecordProgressCounters(state, actor, result);

        return result;
    }

    private static void RecordProgressCounters(GameSessionState state, GamePlayerState actor, CardEffectResult result)
    {
        if (actor.IsBot)
            return;

        switch (result.TelemetryKind)
        {
            case CardEffectTelemetryKind.ZombieCured:
            case CardEffectTelemetryKind.PowerZombieDemoted:
                Increment(state.HealsByPlayer, actor.UserId);
                break;
            case CardEffectTelemetryKind.ZombieInfectionSucceeded:
            case CardEffectTelemetryKind.PowerZombieInfectionSucceeded:
                Increment(state.PoisonsByPlayer, actor.UserId);
                break;
        }
    }

    private static void Increment(Dictionary<Guid, int> counters, Guid playerId)
    {
        counters.TryGetValue(playerId, out var current);
        counters[playerId] = current + 1;
    }

    public void PassAction(GameSessionState state, Guid actorUserId)
    {
        if (state.CurrentPhase != GamePhase.Day)
            throw new ServiceException("Actions are only allowed during the Day phase.");

        var actor = state.GetPlayer(actorUserId)
            ?? throw new ServiceException("Player not found.");

        if (!actor.IsAlive)
            throw new ServiceException("Dead players cannot take actions.");

        if (_settings.MaxPassActionsPerDay > 0 && actor.PassesUsedThisDay >= _settings.MaxPassActionsPerDay)
            throw new ServiceException("Maximum pass actions for this day already used.");

        var isFirstAction = GameCombatRules.IsFirstActionOfTurn(actor);
        ConsumeActionPoint(state, actorUserId);
        actor.PassesUsedThisDay++;

        if (isFirstAction)
            GameCombatRules.CompleteTurnAfterFirstPass(actor);
    }

    private void ConsumeActionPoint(GameSessionState state, Guid actorUserId)
    {
        if (state.CurrentPhase != GamePhase.Day)
            throw new ServiceException("Actions are only allowed during the Day phase.");

        var actor = state.GetPlayer(actorUserId)
            ?? throw new ServiceException("Player not found.");

        if (!actor.IsAlive)
            throw new ServiceException("Dead players cannot take actions.");

        if (actor.RemainingActions <= 0)
            throw new ServiceException("No remaining actions this turn.");
        
        actor.ActionsUsedThisTurn++;
    }

    public void EndDayPhase(GameSessionState state, GameSettings settings)
    {
        if (state.CurrentPhase != GamePhase.Day)
            throw new ServiceException("Not in Day phase.");

        state.CurrentPhase = GamePhase.Discussion;
        state.PhaseEndsAt = DateTime.UtcNow.AddSeconds(settings.DiscussionPhaseSeconds);
        _voting.ClearVotes(state);
    }

    public void CastVote(GameSessionState state, Guid voterId, Guid targetId)
    {
        if (state.CurrentPhase != GamePhase.Voting)
            throw new ServiceException("Voting is only allowed during the Voting phase.");

        _voting.CastVote(state, voterId, targetId);
        BotObservationRecorder.OnVote(state, voterId, targetId);
    }

    public WinTeam? EvaluateImmediateWin(GameSessionState state) => _winConditions.Evaluate(state);

    public async Task CompleteMatchIfWonAsync(GameSessionState state, Match match, CancellationToken cancellationToken = default)
    {
        var winTeam = _winConditions.Evaluate(state);
        if (winTeam is not null)
            await _matchCompletion.CompleteMatchAsync(match, state, winTeam.Value, cancellationToken);
    }

    public PhaseAdvanceResult AdvancePhase(GameSessionState state, Match match, GameSettings settings)
    {
        if (state.IsFinished)
            return new PhaseAdvanceResult(false, "Match is finished.");

        return state.CurrentPhase switch
        {
            GamePhase.Discussion => AdvanceDiscussionToVoting(state, settings),
            GamePhase.Voting => new PhaseAdvanceResult(false, "Voting requires resolution processing."),
            _ => new PhaseAdvanceResult(false, "No automatic phase transition available.")
        };
    }

    public async Task<WinTeam?> ProcessResolutionAsync(GameSessionState state, Match match, CancellationToken cancellationToken = default)
    {
        if (state.CurrentPhase == GamePhase.Voting)
        {
            state.CurrentPhase = GamePhase.Resolution;
            state.PhaseEndsAt = null;
        }

        if (state.CurrentPhase != GamePhase.Resolution)
            return null;

        var eliminatedId = _voting.ResolveElimination(state);
        if (eliminatedId is not null)
        {
            var eliminated = state.GetPlayer(eliminatedId.Value);
            if (eliminated is not null)
            {
                eliminated.IsAlive = false;
                state.Metadata["lastEliminated"] = eliminatedId.Value.ToString();
            }
        }
        else
        {
            state.Metadata["lastEliminated"] = "none";
        }

        var winTeam = _winConditions.Evaluate(state);
        if (winTeam is not null)
        {
            await _matchCompletion.CompleteMatchAsync(match, state, winTeam.Value, cancellationToken);
            return winTeam;
        }

        BeginNextDay(state);
        match.CurrentPhase = state.CurrentPhase;
        return null;
    }

    public bool TryAdvanceVotingToResolution(GameSessionState state, GameSettings settings)
    {
        if (state.CurrentPhase != GamePhase.Voting) return false;

        var timedOut = state.PhaseEndsAt is not null && DateTime.UtcNow >= state.PhaseEndsAt;
        if (!timedOut && !_voting.AllAlivePlayersVoted(state))
            return false;

        state.CurrentPhase = GamePhase.Resolution;
        state.PhaseEndsAt = null;
        return true;
    }

    private PhaseAdvanceResult AdvanceDiscussionToVoting(GameSessionState state, GameSettings settings)
    {
        if (state.PhaseEndsAt is not null && DateTime.UtcNow < state.PhaseEndsAt)
            return new PhaseAdvanceResult(false, "Discussion phase still active.");

        state.CurrentPhase = GamePhase.Voting;
        state.PhaseEndsAt = DateTime.UtcNow.AddSeconds(settings.VotingPhaseSeconds);
        return new PhaseAdvanceResult(true, "Entering voting phase.", GamePhase.Voting);
    }

    private void BeginNextDay(GameSessionState state)
    {
        state.TurnNumber++;
        state.CurrentPhase = GamePhase.Day;
        state.PhaseEndsAt = null;
        _voting.ClearVotes(state);

        state.CurrentDayEvent = _dayEvents.PickRandomEvent();
        state.Metadata["dayEvent"] = state.CurrentDayEvent.ToString();

        ResetDailyCombatState(state);
        ResetActionPoints(state);
        _dealing.ReplenishInventory(state);
    }

    private void ResetActionPoints(GameSessionState state)
    {
        foreach (var player in state.AlivePlayers)
        {
            player.ActionsUsedThisTurn = 0;
            player.PassesUsedThisDay = 0;
            player.ActionsPerTurn = _settings.ActionsPerTurn;
        }
    }

    private static void ResetDailyCombatState(GameSessionState state)
    {
        foreach (var player in state.AlivePlayers)
        {
            player.ShotgunHitCount = 0;
            player.HasRevealedThisDay = false;
            if (player.Role == PlayerRole.PowerZombie)
                player.RemainingHealth = 2;
        }
    }
}
