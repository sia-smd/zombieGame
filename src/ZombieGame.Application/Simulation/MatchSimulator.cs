namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.Bots;
using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.Bots.Discussion;
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
    private readonly ICardDealingService _dealing;
    private readonly ICardConsumptionService _consumption;
    private readonly SimulationBotBrain _botBrain;
    private readonly BotDiscussionEngine _discussionEngine = new();
    private readonly ICardRegistry _cardRegistry;
    private readonly ConfigurableRoleAssignmentService _roleAssignment;
    private readonly GameSettings _settings;

    public MatchSimulator(
        MatchSimulationOptions defaults,
        DayEventOptions? dayEventOptions = null,
        GameSettings? gameSettings = null)
    {
        _roleAssignment = new ConfigurableRoleAssignmentService
        {
            Composition = BalanceScenarioProfiles.GetComposition(defaults.Scenario, defaults.PlayerCount)
        };
        _settings = CloneSettingsForSimulation(gameSettings, defaults);

        _cardRegistry = new InMemoryCardRegistry();
        var transformation = GameRulesComposition.CreateTransformationService(_cardRegistry);
        var dayEvents = GameRulesComposition.CreateDayEventService(dayEventOptions);
        _consumption = new CardConsumptionService();
        var handlers = GameRulesComposition.CreateCardHandlers(dayEvents, transformation, _cardRegistry, _consumption);

        var cardValidator = new CardPlayValidator();
        _botBrain = new SimulationBotBrain(_cardRegistry, cardValidator);
        var generator = new InventoryCardGenerator(_cardRegistry, Options.Create(_settings));
        _dealing = new CardDealingService(generator, Options.Create(_settings));

        _engine = new GameRulesEngine(
            _roleAssignment,
            _dealing,
            dayEvents,
            new CardEffectResolver(handlers, dayEvents),
            cardValidator,
            new VotingService(),
            new WinConditionService(),
            new SimulationMatchCompletionService(),
            Options.Create(_settings));
    }

    public async Task<SingleMatchOutcome> RunSingleMatchAsync(MatchSimulationOptions options, Random random) =>
        (await RunSingleMatchCoreAsync(options, random, recorder: null)).Outcome;

    public async Task<DetailedMatchReplay> RunDetailedReplayAsync(MatchSimulationOptions options, Random random)
    {
        var (outcome, replay) = await RunSingleMatchCoreAsync(options, random, new MatchReplayRecorder());
        replay!.Finalize(outcome.Winner, outcome.IsStalemate, outcome.StartingDayEvent);
        return replay.Report;
    }

    private async Task<(SingleMatchOutcome Outcome, MatchReplayRecorder? Recorder)> RunSingleMatchCoreAsync(
        MatchSimulationOptions options,
        Random random,
        MatchReplayRecorder? recorder)
    {
        var match = BuildMatch(options.PlayerCount);
        var state = BuildState(match);
        var stats = new MatchRunStats();
        var nameLookup = state.Players.ToDictionary(p => p.UserId, p => p.Username);

        _roleAssignment.Composition = BalanceScenarioProfiles.GetComposition(options.Scenario, options.PlayerCount);

        _engine.StartMatch(state, match);
        BotObservationRecorder.InitializeBots(state, random, options.Scenario);

        recorder?.Init(match.Id, options.PlayerCount, options.RandomSeed);
        recorder?.RecordRoster(state.Players.Select(p => (p.UserId, p.Username, p.SeatIndex, p.Role)));

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
            stats.RecordDayEvent(state.CurrentDayEvent);

            var cardsNote = state.TurnNumber == 1
                ? "Initial inventory: 2 slot(s) filled per player"
                : "Empty inventory slot(s) replenished at day start";

            recorder?.BeginDay(
                state.TurnNumber,
                state.CurrentDayEvent,
                state.AlivePlayers.Count(),
                cardsNote);
            recorder?.RecordStartingHands(BuildHandSnapshots(state));

            await RunRoomDayFlowAsync(state, match, options, random, stats, recorder, nameLookup);
            if (state.IsFinished)
            {
                recorder?.RecordGameEnd($"Game ended after Day {state.TurnNumber} battles — Winner: {state.WinTeam}");
                break;
            }

            if (state.CurrentPhase == GamePhase.Day)
                _engine.EndDayPhase(state, _settings);

            if (state.CurrentPhase == GamePhase.Discussion)
                RunDiscussionPhase(state, random, recorder, nameLookup);

            state.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
            _engine.AdvancePhase(state, match, _settings);

            RunVotingPhase(state, options, random, stats, recorder, nameLookup);
            if (state.CurrentPhase == GamePhase.Voting)
                _engine.TryAdvanceVotingToResolution(state, _settings);

            var eliminatedBefore = state.AlivePlayers.Count();
            var voteSnapshot = state.Votes.ToDictionary(v => v.Key, v => v.Value);
            await _engine.ProcessResolutionAsync(state, match);
            RecordVoteElimination(state, stats);
            RecordReplayElimination(state, recorder, nameLookup, voteSnapshot);

            if (state.AlivePlayers.Count() < eliminatedBefore)
                stats.VoteEliminations++;

            recorder?.EndDay(BuildPlayerStatuses(state));

            if (state.IsFinished)
            {
                recorder?.RecordGameEnd($"Game finished after Day {state.TurnNumber} vote — Winner: {state.WinTeam}");
                break;
            }
        }

        var stalemate = !state.IsFinished;
        var finalStatuses = BuildPlayerStatuses(state);
        recorder?.SetFinalStatuses(finalStatuses);

        var outcome = new SingleMatchOutcome
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
            CardPlayCounts = new RoleCardPlayCounts
            {
                Human = stats.CardPlayCounts.Human,
                Zombie = stats.CardPlayCounts.Zombie,
                PowerZombie = stats.CardPlayCounts.PowerZombie
            },
            DayEventCounts = new Dictionary<string, int>(stats.DayEventCounts, StringComparer.OrdinalIgnoreCase),
            HumanPlayersAtStart = stats.HumanPlayersAtStart,
            ZombiePlayersAtStart = stats.ZombiePlayersAtStart,
            PowerZombiePlayersAtStart = stats.PowerZombiePlayersAtStart,
            InfectionCounts = InfectionTelemetryRecorder.ToImmutable(stats.InfectionCounts),
            VotingCounts = VotingTelemetryRecorder.ToImmutable(stats.VotingCounts)
        };

        return (outcome, recorder);
    }

    private static List<ReplayPlayerStatus> BuildPlayerStatuses(GameSessionState state) =>
        state.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new ReplayPlayerStatus
            {
                Name = p.Username,
                Role = p.Role,
                IsAlive = p.IsAlive,
                HandSize = state.PlayerHands.FirstOrDefault(h => h.UserId == p.UserId)?.InventoryCount() ?? 0
            })
            .ToList();

    private static void RecordReplayElimination(
        GameSessionState state,
        MatchReplayRecorder? recorder,
        Dictionary<Guid, string> names,
        Dictionary<Guid, Guid> voteSnapshot)
    {
        if (recorder is null)
            return;

        if (!state.Metadata.TryGetValue("lastEliminated", out var value))
            return;

        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text == "none")
        {
            recorder.RecordElimination(null, null, none: true, BuildVoteCounts(voteSnapshot, names));
            return;
        }

        if (!Guid.TryParse(text, out var eliminatedId))
            return;

        var eliminated = state.Players.FirstOrDefault(p => p.UserId == eliminatedId);
        var eliminatedName = names.GetValueOrDefault(eliminatedId, eliminatedId.ToString()[..8]);
        recorder.RecordElimination(eliminatedName, eliminated?.Role, none: false, BuildVoteCounts(voteSnapshot, names));
    }

    private static Dictionary<string, int> BuildVoteCounts(Dictionary<Guid, Guid> votes, Dictionary<Guid, string> names)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in votes.Where(v => v.Value != Guid.Empty).GroupBy(v => v.Value))
        {
            var targetName = names.GetValueOrDefault(group.Key, group.Key.ToString()[..8]);
            counts[targetName] = group.Count();
        }
        return counts;
    }

    private async Task RunRoomDayFlowAsync(
        GameSessionState state,
        Match match,
        MatchSimulationOptions options,
        Random random,
        MatchRunStats stats,
        MatchReplayRecorder? recorder,
        Dictionary<Guid, string> names)
    {
        BotObservationRecorder.OnDayStart(state);

        var alive = state.AlivePlayers.OrderBy(_ => random.Next()).ToList();
        while (alive.Count >= 2)
        {
            var playerA = alive[0];
            var playerB = alive[1];
            alive.RemoveRange(0, 2);

            ResetBattleTurn(playerA);
            ResetBattleTurn(playerB);
            recorder?.BeginBattle(playerA.Username, playerB.Username);
            await RunPrivateBattleAsync(state, match, playerA, playerB, options, random, stats, recorder, names);
            if (state.IsFinished)
                return;
        }

        if (alive.Count == 1)
            recorder?.RecordUnmatched(alive[0].Username);

        BotObservationRecorder.OnDayEnd(state);
    }

    private async Task RunPrivateBattleAsync(
        GameSessionState state,
        Match match,
        GamePlayerState playerA,
        GamePlayerState playerB,
        MatchSimulationOptions options,
        Random random,
        MatchRunStats stats,
        MatchReplayRecorder? recorder,
        Dictionary<Guid, string> names)
    {
        var witnesses = new[] { playerA.UserId, playerB.UserId };
        foreach (var bot in new[] { playerA, playerB }.OrderBy(_ => random.Next()))
        {
            while (bot.IsAlive && !state.IsFinished && bot.RemainingActions > 0)
            {
                var opponentId = bot.UserId == playerA.UserId ? playerB.UserId : playerA.UserId;
                var opponent = state.GetPlayer(opponentId);
                if (opponent is null || !opponent.IsAlive)
                    break;

                if (_botBrain.ShouldPassBattle(state, bot, random, opponentId))
                {
                    TryPassTurn(state, bot, stats, options, witnesses, recorder);
                    continue;
                }

                var play = _botBrain.TryFindCardPlay(state, bot, random, opponentId);
                var card = play is null ? null : _cardRegistry.GetById(play.CardId);
                var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == bot.UserId);
                if (play is null || card is null || hand is null || !hand.ContainsCard(play.CardId))
                {
                    TryPassTurn(state, bot, stats, options, witnesses, recorder);
                    continue;
                }

                try
                {
                    var targetBefore = state.GetPlayer(play.TargetUserId);
                    var effectResult = _engine.PlayCard(state, bot.UserId, card, play.TargetUserId);
                    _consumption.ConsumeAfterPlay(hand, card);

                    var targetName = names.GetValueOrDefault(play.TargetUserId, play.TargetUserId.ToString()[..8]);
                    recorder?.RecordBattlePlay(bot.Username, card.Name, targetName, effectResult.Message);

                    stats.TotalCardPlays++;
                    stats.CardPlaysByEffect[card.EffectKey] = stats.CardPlaysByEffect.GetValueOrDefault(card.EffectKey) + 1;
                    stats.RecordCardPlay(bot.Role);
                    InfectionTelemetryRecorder.Record(
                        stats.InfectionCounts,
                        card.EffectKey,
                        effectResult.TelemetryKind,
                        effectResult.InfectionTransform);
                    BotObservationRecorder.OnCardOutcome(
                        state, bot.UserId, play.TargetUserId, effectResult, card.EffectKey, witnesses);

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
                    TryPassTurn(state, bot, stats, options, witnesses, recorder);
                }
            }
        }
    }

    private void ResetBattleTurn(GamePlayerState player)
    {
        player.ActionsUsedThisTurn = 0;
        player.ActionsPerTurn = Math.Max(1, _settings.ActionsPerTurn);
    }

    private void TryPassTurn(
        GameSessionState state,
        GamePlayerState bot,
        MatchRunStats stats,
        MatchSimulationOptions options,
        IEnumerable<Guid> witnesses,
        MatchReplayRecorder? recorder)
    {
        try
        {
            _engine.PassAction(state, bot.UserId);
        }
        catch
        {
            bot.ActionsUsedThisTurn = bot.ActionsPerTurn;
        }

        RecordPass(state, bot, stats, options, witnesses);
        recorder?.RecordBattlePass(bot.Username);
    }

    private static GameSettings CloneSettingsForSimulation(GameSettings? source, MatchSimulationOptions defaults)
    {
        var settings = source is null
            ? new GameSettings()
            : new GameSettings
            {
                ActionsPerTurn = source.ActionsPerTurn,
                MaxPassActionsPerDay = source.MaxPassActionsPerDay,
                CardDistribution = source.CardDistribution,
                Inventory = source.Inventory
            };

        settings.DiscussionPhaseSeconds = 0;
        settings.VotingPhaseSeconds = 0;
        settings.ActionsPerTurn = Math.Max(1, settings.ActionsPerTurn);
        settings.MaxPassActionsPerDay = defaults.MaxPassActionsPerDay;
        return settings;
    }

    private void RunVotingPhase(
        GameSessionState state,
        MatchSimulationOptions options,
        Random random,
        MatchRunStats stats,
        MatchReplayRecorder? recorder,
        Dictionary<Guid, string> names)
    {
        if (state.CurrentPhase != GamePhase.Voting) return;

        foreach (var bot in state.AlivePlayers.OrderBy(_ => random.Next()).ToList())
        {
            try
            {
                var target = _botBrain.DecideVoteTarget(state, bot, random, options.UseSuspicionBasedVoting);
                _engine.CastVote(state, bot.UserId, target);
                VotingTelemetryRecorder.RecordVote(stats.VotingCounts, state, bot.UserId, target);
                recorder?.RecordVote(
                    bot.Username,
                    names.GetValueOrDefault(target, target.ToString()[..8]));
            }
            catch
            {
                // skip invalid vote
            }
        }
    }

    private void RunDiscussionPhase(
        GameSessionState state,
        Random random,
        MatchReplayRecorder? recorder,
        Dictionary<Guid, string> names)
    {
        var announcements = DiscussionAnnouncementService.Build(state);
        DiscussionEventPublisher.PublishAnnouncements(state, announcements);
        foreach (var announcement in announcements)
            recorder?.RecordDiscussionAnnouncement(DiscussionMessageFormatter.FormatAnnouncement(announcement));

        var bots = state.AlivePlayers.Where(p => p.IsBot).OrderBy(_ => random.Next()).ToList();
        foreach (var bot in bots)
        {
            if (!_discussionEngine.ShouldSpeak(state, bot.UserId, random))
                continue;

            var message = _discussionEngine.DecideMessage(state, bot.UserId, random);
            if (message is null)
                continue;

            var targetName = message.TargetUserId is Guid tid
                ? names.GetValueOrDefault(tid, tid.ToString()[..8])
                : null;
            var speakerName = names.GetValueOrDefault(bot.UserId, bot.Username);

            DiscussionEventPublisher.Publish(state, message, speakerName, targetName);
            _discussionEngine.RecordSpoke(state, bot.UserId);
            recorder?.RecordDiscussion(
                speakerName,
                message.MessageType,
                targetName,
                DiscussionMessageFormatter.Format(speakerName, message.MessageType, targetName));
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
            BotObservationRecorder.OnElimination(state, eliminatedId, eliminated.Role);
        }
    }

    private static void RecordPass(
        GameSessionState state,
        GamePlayerState bot,
        MatchRunStats stats,
        MatchSimulationOptions options,
        IEnumerable<Guid>? witnesses = null)
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

        BotObservationRecorder.OnPass(state, bot.UserId, witnesses);
    }

    private IEnumerable<ReplayPlayerHandSnapshot> BuildHandSnapshots(GameSessionState state) =>
        state.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p =>
            {
                var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == p.UserId);
                string? NameFor(Guid? id) =>
                    id is null ? null : _cardRegistry.GetById(id.Value)?.Name ?? id.Value.ToString()[..8];

                return new ReplayPlayerHandSnapshot
                {
                    Name = p.Username,
                    SeatIndex = p.SeatIndex,
                    Role = p.Role,
                    IsAlive = p.IsAlive,
                    RoleCard = hand is null ? null : NameFor(hand.RoleCardId),
                    InventorySlot1 = hand is null ? null : NameFor(hand.InventorySlot1),
                    InventorySlot2 = hand is null ? null : NameFor(hand.InventorySlot2)
                };
            });

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
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var players = match.Players.OrderBy(p => p.SeatIndex).Select((p, i) => new GamePlayerState
        {
            UserId = p.UserId,
            Username = BotNameCatalog.NextUnique(reserved, Random.Shared),
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
        public MutableRoleCardPlayCounts CardPlayCounts { get; } = new();
        public Dictionary<string, int> DayEventCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public MutableInfectionMatchCounts InfectionCounts { get; } = new();
        public MutableVotingMatchCounts VotingCounts { get; } = new();
        public Dictionary<string, int> CardPlaysByEffect { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void RecordDayEvent(DayEventType dayEvent)
        {
            var key = dayEvent.ToString();
            DayEventCounts[key] = DayEventCounts.GetValueOrDefault(key) + 1;
        }

        public void RecordCardPlay(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Human:
                    CardPlayCounts.Human++;
                    break;
                case PlayerRole.Zombie:
                    CardPlayCounts.Zombie++;
                    break;
                case PlayerRole.PowerZombie:
                    CardPlayCounts.PowerZombie++;
                    break;
            }
        }
    }

    private sealed class MutableRoleCardPlayCounts
    {
        public int Human { get; set; }
        public int Zombie { get; set; }
        public int PowerZombie { get; set; }
    }

    private sealed class MutableRolePassCounts
    {
        public int Human { get; set; }
        public int Zombie { get; set; }
        public int PowerZombie { get; set; }
    }
}
