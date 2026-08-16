namespace ZombieGame.Application.Tests.GameRules;

using ZombieGame.Application.Common;
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

public class DayIdentityRevealTests
{
    private readonly GameRulesEngine _engine;
    private readonly CardEffectResolver _resolver;

    public DayIdentityRevealTests()
    {
        var dayEvents = GameTestBuilder.CreateDayEventService();
        var handlers = GameTestBuilder.CreateCardHandlers(dayEvents);
        _resolver = new CardEffectResolver(handlers, dayEvents);
        _engine = new GameRulesEngine(
            new RoleAssignmentService(),
            GameTestBuilder.CreateDealingService(),
            dayEvents,
            _resolver,
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new MatchCompletionService(new FakeCoinService(), new FakeMatchRepository(new Match { Id = Guid.NewGuid() }), new FakeUnitOfWork()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));
    }

    [Fact]
    public void Heal_NoEffectOnHiddenZombie()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = CreateDayState(humanId, zombieId);
        state.Player(zombieId).HasRevealedThisDay = false;
        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Heal);

        var result = PlayCard(state, humanId, TestCards.Heal, zombieId);

        Assert.True(result.Success);
        Assert.False(result.RoleChanged);
        Assert.Equal(PlayerRole.Zombie, state.Player(zombieId).Role);
    }

    [Fact]
    public void Heal_CuresZombie_WhenZombieRevealedByAttack()
    {
        var humanId = Guid.NewGuid();
        var victimId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (victimId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Day;
        SetActionPoints(state);

        PlayCard(state, zombieId, TestCards.Infection, victimId);
        Assert.True(state.Player(zombieId).HasRevealedThisDay);

        var result = PlayCard(state, humanId, TestCards.Heal, zombieId);

        Assert.True(result.Success);
        Assert.True(result.RoleChanged);
        Assert.Equal(PlayerRole.Human, state.Player(zombieId).Role);
    }

    [Fact]
    public void Shotgun_HitsHiddenZombie_WithoutReveal()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = CreateDayState(humanId, zombieId);
        state.Player(zombieId).HasRevealedThisDay = false;
        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Shotgun);

        var result = PlayCard(state, humanId, TestCards.Shotgun, zombieId);

        Assert.True(result.Success);
        Assert.False(state.Player(zombieId).IsAlive);
    }

    [Fact]
    public void Shotgun_KillsZombie_WhenZombieRevealedByAttack()
    {
        var humanId = Guid.NewGuid();
        var victimId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            DayEventType.NormalDay,
            (humanId, PlayerRole.Human, true),
            (victimId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Day;
        SetActionPoints(state);

        PlayCard(state, zombieId, TestCards.Infection, victimId);

        var result = PlayCard(state, humanId, TestCards.Shotgun, zombieId);

        Assert.True(result.TargetKilled);
        Assert.False(state.Player(zombieId).IsAlive);
    }

    [Fact]
    public void Infect_RevealsZombie_EvenWhenBlockedByShield()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.Player(humanId).HasShield = true;

        _resolver.Play(new CardEffectContext
        {
            State = state,
            ActorUserId = zombieId,
            TargetUserId = humanId,
            Card = TestCards.Infection
        });

        Assert.True(state.Player(zombieId).HasRevealedThisDay);
    }

    [Fact]
    public void Pass_DoesNotRevealPlayer()
    {
        var zombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((zombieId, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Day;
        SetActionPoints(state);

        _engine.PassAction(state, zombieId);

        Assert.False(state.Player(zombieId).HasRevealedThisDay);
    }

    [Fact]
    public async Task HasRevealedThisDay_ResetsOnNewDay()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.InProgress,
            Players =
            [
                new MatchPlayer { UserId = p1 },
                new MatchPlayer { UserId = p2 },
                new MatchPlayer { UserId = zombieId }
            ]
        };

        var state = GameTestBuilder.CreateSession(
            (p1, PlayerRole.Human, true),
            (p2, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Voting;
        state.Player(zombieId).HasRevealedThisDay = true;

        state.CurrentPhase = GamePhase.Resolution;
        await _engine.ProcessResolutionAsync(state, match);

        Assert.False(state.Player(zombieId).HasRevealedThisDay);
    }

    private static GameSessionState CreateDayState(Guid humanId, Guid zombieId)
    {
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Day;
        SetActionPoints(state);
        return state;
    }

    private static void SetActionPoints(GameSessionState state)
    {
        foreach (var player in state.Players)
        {
            player.ActionsPerTurn = 2;
            player.ActionsUsedThisTurn = 0;
        }
    }

    private CardEffectResult PlayCard(GameSessionState state, Guid actorId, CardDefinition card, Guid targetId)
    {
        foreach (var player in state.Players)
        {
            player.ActionsPerTurn = Math.Max(player.ActionsPerTurn, 2);
        }

        return _engine.PlayCard(state, actorId, card, targetId);
    }
}
