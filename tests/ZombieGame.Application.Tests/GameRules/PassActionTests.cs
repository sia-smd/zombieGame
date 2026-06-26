namespace ZombieGame.Application.Tests.GameRules;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Application.Simulation;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public class PassActionTests
{
    private readonly GameRulesEngine _engine;
    private readonly GameSettings _settings = new() { ActionsPerTurn = 2, MaxPassActionsPerDay = 0 };

    public PassActionTests()
    {
        var dayEvents = GameTestBuilder.CreateDayEventService();
        var handlers = GameTestBuilder.CreateCardHandlers(dayEvents);

        _engine = new GameRulesEngine(
            new RoleAssignmentService(),
            new CardDealingService(new InMemoryCardRegistry(), Options.Create(_settings)),
            dayEvents,
            new CardEffectResolver(handlers, dayEvents),
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new SimulationMatchCompletionService(),
            Options.Create(_settings));
    }

    [Fact]
    public void PassConsumesOneAction()
    {
        var playerId = Guid.NewGuid();
        var state = CreateDayState(playerId);

        _engine.PassAction(state, playerId);

        Assert.Equal(1, state.Player(playerId).ActionsUsedThisTurn);
        Assert.Equal(1, state.Player(playerId).RemainingActions);
    }

    [Fact]
    public void DoublePassConsumesFullTurn()
    {
        var playerId = Guid.NewGuid();
        var state = CreateDayState(playerId);

        _engine.PassAction(state, playerId);
        _engine.PassAction(state, playerId);

        Assert.Equal(2, state.Player(playerId).ActionsUsedThisTurn);
        Assert.Equal(0, state.Player(playerId).RemainingActions);
    }

    [Fact]
    public void SecondPassRejectedWhenMaxPassActionsPerDayIsOne()
    {
        var limitedSettings = new GameSettings { ActionsPerTurn = 2, MaxPassActionsPerDay = 1 };
        var dayEvents = GameTestBuilder.CreateDayEventService();
        var engine = new GameRulesEngine(
            new RoleAssignmentService(),
            new CardDealingService(new InMemoryCardRegistry(), Options.Create(limitedSettings)),
            dayEvents,
            new CardEffectResolver(GameTestBuilder.CreateCardHandlers(dayEvents), dayEvents),
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new SimulationMatchCompletionService(),
            Options.Create(limitedSettings));

        var playerId = Guid.NewGuid();
        var state = CreateDayState(playerId);

        engine.PassAction(state, playerId);

        var ex = Assert.Throws<ServiceException>(() => engine.PassAction(state, playerId));
        Assert.Contains("Maximum pass actions", ex.Message);
        Assert.Equal(1, state.Player(playerId).ActionsUsedThisTurn);
        Assert.Equal(1, state.Player(playerId).RemainingActions);
    }

    [Fact]
    public void ThirdActionRejected()
    {
        var playerId = Guid.NewGuid();
        var state = CreateDayState(playerId);

        _engine.PassAction(state, playerId);
        _engine.PassAction(state, playerId);

        var ex = Assert.Throws<ServiceException>(() => _engine.PassAction(state, playerId));
        Assert.Contains("No remaining actions", ex.Message);
    }

    [Fact]
    public void PassDoesNotConsumeCards()
    {
        var playerId = Guid.NewGuid();
        var state = CreateDayState(playerId);
        var hand = state.PlayerHands.First(h => h.UserId == playerId);
        hand.CardIds.AddRange([TestCards.Shotgun.Id, TestCards.Heal.Id, TestCards.Shield.Id]);

        _engine.PassAction(state, playerId);

        Assert.Equal(3, hand.CardIds.Count);
    }

    [Fact]
    public void PassOnlyAllowedDuringDayPhase()
    {
        var playerId = Guid.NewGuid();
        var state = CreateDayState(playerId);
        state.CurrentPhase = GamePhase.Discussion;

        var ex = Assert.Throws<ServiceException>(() => _engine.PassAction(state, playerId));
        Assert.Contains("Day phase", ex.Message);
    }

    [Fact]
    public void PlayCardAlsoConsumesActionPoint()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        foreach (var player in state.Players)
        {
            player.ActionsPerTurn = 2;
            player.ActionsUsedThisTurn = 0;
        }
        state.Player(zombieId).HasRevealedThisDay = true;

        var hand = state.PlayerHands.First(h => h.UserId == humanId);
        hand.CardIds.Add(TestCards.Shotgun.Id);

        _engine.PlayCard(state, humanId, TestCards.Shotgun, zombieId);

        Assert.Equal(1, state.Player(humanId).RemainingActions);
    }

    [Fact]
    public void StartMatch_ResetsActionPointsForAllPlayers()
    {
        var match = new Match { Id = Guid.NewGuid(), MaxPlayers = 4 };
        var state = new GameSessionState
        {
            MatchId = match.Id,
            Players = Enumerable.Range(0, 4).Select(i => new GamePlayerState
            {
                UserId = Guid.NewGuid(),
                Username = $"P{i}",
                SeatIndex = i,
                IsAlive = true
            }).ToList()
        };
        state.PlayerHands = state.Players.Select(p => new PlayerCardState { UserId = p.UserId }).ToList();
        foreach (var p in state.Players)
            match.Players.Add(new MatchPlayer { UserId = p.UserId, SeatIndex = p.SeatIndex });

        _engine.StartMatch(state, match);

        Assert.All(state.Players, p =>
        {
            Assert.Equal(2, p.ActionsPerTurn);
            Assert.Equal(0, p.ActionsUsedThisTurn);
            Assert.Equal(2, p.RemainingActions);
        });
    }

    private static GameSessionState CreateDayState(params Guid[] playerIds)
    {
        var state = GameTestBuilder.CreateSession(
            playerIds.Select((id, i) => (id, PlayerRole.Human, true)).ToArray());

        foreach (var player in state.Players)
        {
            player.ActionsPerTurn = 2;
            player.ActionsUsedThisTurn = 0;
        }

        return state;
    }
}
