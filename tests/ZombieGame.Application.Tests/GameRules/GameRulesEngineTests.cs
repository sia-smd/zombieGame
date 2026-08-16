namespace ZombieGame.Application.Tests.GameRules;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Cards.Handlers;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using Microsoft.Extensions.Options;

public class GameRulesEngineTests
{
    private readonly GameRulesEngine _engine;

    public GameRulesEngineTests()
    {
        var dayEvents = GameTestBuilder.CreateDayEventService();
        _engine = CreateEngine(dayEvents, new Match { Id = Guid.NewGuid(), Players = [] });
    }

    [Fact]
    public void StartMatch_AssignsRolesForEightPlayers()
    {
        var match = CreateMatch(8);
        var state = CreateEmptyState(match);

        _engine.StartMatch(state, match);

        Assert.Equal(1, state.Players.Count(p => p.Role == PlayerRole.PowerZombie));
        Assert.Equal(1, state.Players.Count(p => p.Role == PlayerRole.Zombie));
        Assert.Equal(6, state.Players.Count(p => p.Role == PlayerRole.Human));
        Assert.Equal(1, state.TurnNumber);
        Assert.Equal(DayEventType.NormalDay, state.CurrentDayEvent);
    }

    [Fact]
    public void StartMatch_AssignsRolesForSixteenPlayers()
    {
        var match = CreateMatch(16);
        var state = CreateEmptyState(match);

        _engine.StartMatch(state, match);

        Assert.Equal(1, state.Players.Count(p => p.Role == PlayerRole.PowerZombie));
        Assert.Equal(3, state.Players.Count(p => p.Role == PlayerRole.Zombie));
        Assert.Equal(12, state.Players.Count(p => p.Role == PlayerRole.Human));
    }

    [Fact]
    public void PhaseFlow_DiscussionToVoting()
    {
        var settings = new GameSettings { DiscussionPhaseSeconds = 0, VotingPhaseSeconds = 60 };
        var match = CreateMatch(4);
        var state = CreateEmptyState(match);
        _engine.StartMatch(state, match);

        _engine.EndDayPhase(state, settings);
        Assert.Equal(GamePhase.Discussion, state.CurrentPhase);

        state.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
        var advance = _engine.AdvancePhase(state, match, settings);
        Assert.True(advance.Advanced);
        Assert.Equal(GamePhase.Voting, state.CurrentPhase);
    }

    [Fact]
    public void PlayCard_ShotgunKillTriggersHumanWin()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Players =
            [
                new MatchPlayer { UserId = humanId },
                new MatchPlayer { UserId = zombieId }
            ]
        };

        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Day;
        state.Player(zombieId).HasRevealedThisDay = true;
        foreach (var player in state.Players)
        {
            player.ActionsPerTurn = 2;
            player.ActionsUsedThisTurn = 0;
        }

        var engine = CreateEngine(GameTestBuilder.CreateDayEventService(), match);
        engine.PlayCard(state, humanId, TestCards.Shotgun, zombieId);

        Assert.Equal(WinTeam.Humans, engine.EvaluateImmediateWin(state));
    }

    [Fact]
    public async Task ProcessResolution_EliminatesVotedPlayerAndStartsNextDay()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var zombie1 = Guid.NewGuid();
        var zombie2 = Guid.NewGuid();

        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.InProgress,
            Players =
            [
                new MatchPlayer { UserId = p1, IsBot = true },
                new MatchPlayer { UserId = p2, IsBot = true },
                new MatchPlayer { UserId = p3, IsBot = true },
                new MatchPlayer { UserId = zombie1, IsBot = true },
                new MatchPlayer { UserId = zombie2, IsBot = true }
            ]
        };

        var state = GameTestBuilder.CreateSession(
            (p1, PlayerRole.Human, true),
            (p2, PlayerRole.Human, true),
            (p3, PlayerRole.Human, true),
            (zombie1, PlayerRole.Zombie, true),
            (zombie2, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Voting;
        state.TurnNumber = 1;

        var engine = CreateEngine(GameTestBuilder.CreateDayEventService(), match);
        engine.CastVote(state, p1, zombie1);
        engine.CastVote(state, p2, zombie1);
        engine.CastVote(state, p3, zombie1);

        state.CurrentPhase = GamePhase.Resolution;
        var win = await engine.ProcessResolutionAsync(state, match);

        Assert.Null(win);
        Assert.False(state.Player(zombie1).IsAlive);
        Assert.True(state.Player(zombie2).IsAlive);
        Assert.Equal(GamePhase.Day, state.CurrentPhase);
        Assert.Equal(2, state.TurnNumber);
    }

    private static GameRulesEngine CreateEngine(IDayEventService dayEvents, Match match)
    {
        var handlers = GameTestBuilder.CreateCardHandlers(dayEvents);
        return new GameRulesEngine(
            new RoleAssignmentService(),
            GameTestBuilder.CreateDealingService(),
            dayEvents,
            new CardEffectResolver(handlers, dayEvents),
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new MatchCompletionService(new FakeCoinService(), new FakeMatchRepository(match), new FakeUnitOfWork()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));
    }

    private static Match CreateMatch(int count)
    {
        var match = new Match { Id = Guid.NewGuid(), MaxPlayers = count };
        for (var i = 0; i < count; i++)
            match.Players.Add(new MatchPlayer { UserId = Guid.NewGuid(), SeatIndex = i });
        return match;
    }

    private static GameSessionState CreateEmptyState(Match match) =>
        new()
        {
            MatchId = match.Id,
            Players = match.Players.Select((p, i) => new GamePlayerState
            {
                UserId = p.UserId,
                Username = $"P{i}",
                SeatIndex = i,
                Role = PlayerRole.Human,
                IsAlive = true
            }).ToList(),
            PlayerHands = match.Players.Select(p => new PlayerCardState
            {
                UserId = p.UserId,
                RoleCardId = Domain.Cards.RoleCardCatalog.Human
            }).ToList()
        };
}
