namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.Bots;
using ZombieGame.Application.Common;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using Microsoft.Extensions.Options;

public sealed class MatchSimulator
{
    private readonly IGameRulesEngine _engine;
    private readonly SimulationBotBrain _botBrain;
    private readonly ICardRegistry _cardRegistry;
    private readonly GameSettings _settings;

    public MatchSimulator(MatchSimulationOptions defaults)
    {
        _settings = new GameSettings
        {
            InitialHandSize = 3,
            CardsDealtPerDay = 1,
            DiscussionPhaseSeconds = 0,
            VotingPhaseSeconds = 0,
            ActionsPerTurn = 2,
            MaxPassActionsPerDay = defaults.MaxPassActionsPerDay,
            ShieldBlocksPowerZombieInfection = defaults.ShieldBlocksPowerZombieInfection
        };

        _cardRegistry = new InMemoryCardRegistry();
        var transformation = new InfectionTransformationService(_cardRegistry);
        var dayEvents = new DayEventService(new IDayEventModifier[]
        {
            new NormalDayModifier(),
            new SunnyDayModifier(),
            new StormDayModifier()
        });

        var handlers = new ICardEffectHandler[]
        {
            new ShotgunHandler(dayEvents),
            new HealHandler(transformation),
            new ShieldHandler(),
            new ZombieInfectionHandler(transformation),
            new PowerZombieInfectionHandler(transformation, _settings.ShieldBlocksPowerZombieInfection)
        };

        var cardValidator = new CardPlayValidator();
        _botBrain = new SimulationBotBrain(_cardRegistry, cardValidator);

        _engine = new GameRulesEngine(
            new RoleAssignmentService(),
            new CardDealingService(_cardRegistry, Options.Create(_settings)),
            dayEvents,
            new CardEffectResolver(handlers, dayEvents),
            cardValidator,
            new VotingService(),
            new WinConditionService(),
            new SimulationMatchCompletionService(),
            Options.Create(_settings));
    }

    public async Task<SingleMatchOutcome> RunSingleMatchAsync(MatchSimulationOptions options, Random random)
    {
        var match = BuildMatch(options.PlayerCount);
        var state = BuildState(match);
        var stats = new MatchRunStats();

        _engine.StartMatch(state, match);
        SuspicionScoring.EnsureInitialized(state);
        stats.VotingCounts.StartingZombies = state.Players.Count(p => p.Role == PlayerRole.Zombie);
        stats.VotingCounts.StartingPowerZombies = state.Players.Count(p => p.Role == PlayerRole.PowerZombie);
        stats.StartingDayEvent = state.CurrentDayEvent;
        stats.HumanPlayersAtStart = state.Players.Count(p => p.Role == PlayerRole.Human);
        stats.ZombiePlayersAtStart = state.Players.Count(p => p.Role == PlayerRole.Zombie);
        stats.PowerZombiePlayersAtStart = state.Players.Count(p => p.Role == PlayerRole.PowerZombie);
        foreach (var role in state.Players.Select(p => p.Role).Distinct())
            stats.StartingRolesPresent[role] = true;

        while (!state.IsFinished && state.TurnNumber <= options.MaxTurnsPerMatch)
        {
            await RunDayPhaseAsync(state, match, options, random, stats);
            if (state.IsFinished) break;

            if (state.CurrentPhase == GamePhase.Day)
                _engine.EndDayPhase(state, _settings);

            state.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
            _engine.AdvancePhase(state, match, _settings);

            RunVotingPhase(state, options, random, stats);
            if (state.CurrentPhase == GamePhase.Voting)
                _engine.TryAdvanceVotingToResolution(state, _settings);

            var eliminatedBefore = state.AlivePlayers.Count();
            await _engine.ProcessResolutionAsync(state, match);
            RecordVoteElimination(state, stats);
            if (state.AlivePlayers.Count() < eliminatedBefore)
                stats.VoteEliminations++;

            if (state.IsFinished) break;
        }

        var stalemate = !state.IsFinished;
        return new SingleMatchOutcome
        {
            Winner = stalemate ? null : state.WinTeam,
            IsStalemate = stalemate,
            Turns = state.TurnNumber,
            FriendlyFireCount = state.FriendlyFireCount,
            VoteEliminations = stats.VoteEliminations,
            TotalCardPlays = stats.TotalCardPlays,
            StartingDayEvent = stats.StartingDayEvent,
            FinalDayEvent = state.CurrentDayEvent,
            StartingRolesPresent = stats.StartingRolesPresent,
            CardPlaysByEffect = new Dictionary<string, int>(stats.CardPlaysByEffect, StringComparer.OrdinalIgnoreCase),
            PassCounts = new RolePassCounts
            {
                Human = stats.PassCounts.Human,
                Zombie = stats.PassCounts.Zombie,
                PowerZombie = stats.PassCounts.PowerZombie
            },
            HumanPlayersAtStart = stats.HumanPlayersAtStart,
            ZombiePlayersAtStart = stats.ZombiePlayersAtStart,
            PowerZombiePlayersAtStart = stats.PowerZombiePlayersAtStart,
            InfectionCounts = InfectionTelemetryRecorder.ToImmutable(stats.InfectionCounts),
            VotingCounts = VotingTelemetryRecorder.ToImmutable(stats.VotingCounts)
        };
    }

    private async Task RunDayPhaseAsync(
        GameSessionState state,
        Match match,
        MatchSimulationOptions options,
        Random random,
        MatchRunStats stats)
    {
        SuspicionScoring.ResetDayActivities(state);

        foreach (var bot in state.AlivePlayers.OrderBy(_ => random.Next()).ToList())
        {
            while (bot.IsAlive && bot.RemainingActions > 0 && state.CurrentPhase == GamePhase.Day && !state.IsFinished)
            {
                var play = _botBrain.TryFindCardPlay(state, bot, random);
                if (play is not null)
                {
                    var card = _cardRegistry.GetById(play.CardId);
                    var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == bot.UserId);
                    if (card is null || hand is null || !hand.CardIds.Contains(play.CardId)) 
                    {
                        if (!TryExecutePass(state, bot, stats, options))
                            break;
                        continue;
                    }

                    try
                    {
                        var targetBefore = state.GetPlayer(play.TargetUserId);
                        var effectResult = _engine.PlayCard(state, bot.UserId, card, play.TargetUserId);
                        hand.CardIds.Remove(play.CardId);
                        stats.TotalCardPlays++;
                        stats.CardPlaysByEffect[card.EffectKey] = stats.CardPlaysByEffect.GetValueOrDefault(card.EffectKey) + 1;
                        InfectionTelemetryRecorder.Record(
                            stats.InfectionCounts,
                            card.EffectKey,
                            effectResult.TelemetryKind,
                            effectResult.InfectionTransform);
                        SuspicionScoring.ApplyCardOutcome(state, bot.UserId, play.TargetUserId, effectResult, card.EffectKey);

                        if (effectResult.TargetKilled && targetBefore?.Role == PlayerRole.PowerZombie)
                            stats.InfectionCounts.PowerZombieEliminations++;

                        if (_engine.EvaluateImmediateWin(state) is not null)
                        {
                            await _engine.CompleteMatchIfWonAsync(state, match);
                            return;
                        }
                    }
                    catch
                    {
                        if (!TryExecutePass(state, bot, stats, options))
                            break;
                    }
                }
                else
                {
                    if (!TryExecutePass(state, bot, stats, options))
                        break;
                }
            }
        }

        SuspicionScoring.FinalizeDay(state);
    }

    private void RunVotingPhase(
        GameSessionState state,
        MatchSimulationOptions options,
        Random random,
        MatchRunStats stats)
    {
        if (state.CurrentPhase != GamePhase.Voting) return;

        foreach (var bot in state.AlivePlayers.OrderBy(_ => random.Next()).ToList())
        {
            try
            {
                var target = _botBrain.DecideVoteTarget(state, bot, random, options.UseSuspicionBasedVoting);
                _engine.CastVote(state, bot.UserId, target);
                VotingTelemetryRecorder.RecordVote(stats.VotingCounts, state, bot.UserId, target);
            }
            catch
            {
                // skip invalid vote
            }
        }
    }

    private static void RecordVoteElimination(GameSessionState state, MatchRunStats stats)
    {
        if (!state.Metadata.TryGetValue("lastEliminated", out var value))
            return;

        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text) || text == "none" || !Guid.TryParse(text, out var eliminatedId))
            return;

        var eliminated = state.Players.FirstOrDefault(p => p.UserId == eliminatedId);
        if (eliminated is not null)
        {
            VotingTelemetryRecorder.RecordElimination(stats.VotingCounts, eliminated);
            if (eliminated.Role == PlayerRole.PowerZombie)
                stats.InfectionCounts.PowerZombieEliminations++;
        }
    }

    private bool TryExecutePass(
        GameSessionState state,
        GamePlayerState bot,
        MatchRunStats stats,
        MatchSimulationOptions options)
    {
        if (_settings.MaxPassActionsPerDay > 0 && bot.PassesUsedThisDay >= _settings.MaxPassActionsPerDay)
            return false;

        RecordPass(state, bot, stats, options);

        try
        {
            _engine.PassAction(state, bot.UserId);
            return true;
        }
        catch (ServiceException ex) when (ex.Message.Contains("Maximum pass actions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    private static void RecordPass(
        GameSessionState state,
        GamePlayerState bot,
        MatchRunStats stats,
        MatchSimulationOptions options)
    {
        switch (bot.Role)
        {
            case PlayerRole.Human:
                stats.PassCounts.Human++;
                break;
            case PlayerRole.Zombie:
                stats.PassCounts.Zombie++;
                break;
            case PlayerRole.PowerZombie:
                stats.PassCounts.PowerZombie++;
                break;
        }

        if (options.PassPenaltyMode)
            bot.InactiveForNextDealing = true;

        SuspicionScoring.RecordPass(state, bot.UserId);
    }

    private static Match BuildMatch(int playerCount)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            MaxPlayers = playerCount,
            Status = MatchStatus.Waiting
        };

        for (var i = 0; i < playerCount; i++)
        {
            match.Players.Add(new MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                UserId = Guid.NewGuid(),
                IsBot = true,
                SeatIndex = i
            });
        }

        return match;
    }

    private static GameSessionState BuildState(Match match)
    {
        var players = match.Players.OrderBy(p => p.SeatIndex).Select((p, i) => new GamePlayerState
        {
            UserId = p.UserId,
            Username = $"Bot_{i}",
            IsBot = true,
            SeatIndex = i,
            IsAlive = true,
            Role = PlayerRole.Human
        }).ToList();

        return new GameSessionState
        {
            MatchId = match.Id,
            SessionToken = Guid.NewGuid().ToString("N"),
            Players = players,
            PlayerHands = players.Select(p => new PlayerCardState { UserId = p.UserId }).ToList()
        };
    }

    private sealed class MatchRunStats
    {
        public DayEventType StartingDayEvent { get; set; }
        public Dictionary<PlayerRole, bool> StartingRolesPresent { get; } = new();
        public int HumanPlayersAtStart { get; set; }
        public int ZombiePlayersAtStart { get; set; }
        public int PowerZombiePlayersAtStart { get; set; }
        public int VoteEliminations { get; set; }
        public int TotalCardPlays { get; set; }
        public MutableRolePassCounts PassCounts { get; } = new();
        public MutableInfectionMatchCounts InfectionCounts { get; } = new();
        public MutableVotingMatchCounts VotingCounts { get; } = new();
        public Dictionary<string, int> CardPlaysByEffect { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class MutableRolePassCounts
    {
        public int Human { get; set; }
        public int Zombie { get; set; }
        public int PowerZombie { get; set; }
    }
}
