namespace ZombieGame.Application.Tests.GameRules;

using ZombieGame.Application.Common;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using Microsoft.Extensions.Options;

public class InfectionTransformationTests
{
    private readonly GameRulesEngine _engine;
    private readonly CardEffectResolver _resolver;
    private readonly InfectionTransformationService _transformation;

    public InfectionTransformationTests()
    {
        var dayEvents = GameTestBuilder.CreateDayEventService();
        _transformation = GameTestBuilder.CreateTransformationService();
        var handlers = GameTestBuilder.CreateCardHandlers(dayEvents, _transformation);
        _resolver = new CardEffectResolver(handlers, dayEvents);
        _engine = new GameRulesEngine(
            new RoleAssignmentService(),
            GameTestBuilder.CreateDealingService(),
            dayEvents,
            _resolver,
            new CardPlayValidator(),
            new VotingService(),
            new WinConditionService(),
            new MatchCompletionService(
                new FakeCoinService(),
                new FakeMatchRepository(new Match { Id = Guid.NewGuid() }),
                new FakeUnitOfWork()),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));
    }

    [Fact]
    public void Infection_ChangesRoleToZombie()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = CreateDayState(humanId, zombieId);

        var result = Infect(state, zombieId, humanId);

        Assert.True(result.RoleChanged);
        Assert.Equal(PlayerRole.Zombie, state.Player(humanId).Role);
        Assert.Equal(PlayerRole.Zombie, state.Player(zombieId).Role);
    }

    [Fact]
    public void InfectedHuman_WithShieldCard_CanStillUseShield()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = CreateDayState(humanId, zombieId);
        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Shield, TestCards.Shotgun, TestCards.Heal);

        Infect(state, zombieId, humanId);
        Assert.Equal(PlayerRole.Zombie, state.Player(humanId).Role);

        var shieldResult = _engine.PlayCard(state, humanId, TestCards.Shield, humanId);

        Assert.True(shieldResult.Success);
        Assert.True(state.Player(humanId).HasShield);
    }

    [Fact]
    public void InfectedHuman_CannotUseShotgun()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var otherHumanId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true),
            (otherHumanId, PlayerRole.Human, true));
        state.CurrentPhase = GamePhase.Day;
        GameTestBuilder.ResetActionPoints(state);
        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Shotgun, TestCards.Shield);

        Infect(state, zombieId, humanId);
        state.Player(otherHumanId).HasRevealedThisDay = true;
        GameTestBuilder.ResetActionPoints(state);

        var hand = state.PlayerHands.First(h => h.UserId == humanId);
        Assert.Equal(TestCards.Shotgun.Id, hand.InventorySlot1);
        Assert.Contains(TestCards.Shotgun.Id, hand.DisabledCardIds);

        var ex = Assert.Throws<ServiceException>(() =>
            _engine.PlayCard(state, humanId, TestCards.Shotgun, otherHumanId));

        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InfectedHuman_CannotUseHeal()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var revealedZombieId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true),
            (revealedZombieId, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Day;
        GameTestBuilder.ResetActionPoints(state);
        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Heal);

        Infect(state, zombieId, humanId);
        state.Player(revealedZombieId).HasRevealedThisDay = true;
        GameTestBuilder.ResetActionPoints(state);

        var hand = state.PlayerHands.First(h => h.UserId == humanId);
        Assert.Equal(TestCards.Heal.Id, hand.InventorySlot1);
        Assert.Contains(TestCards.Heal.Id, hand.DisabledCardIds);

        var ex = Assert.Throws<ServiceException>(() =>
            _engine.PlayCard(state, humanId, TestCards.Heal, revealedZombieId));

        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Infection_DisablesShotgunAndHeal_ButKeepsShieldPlayable()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var state = CreateDayState(humanId, zombieId);
        GameTestBuilder.AddCardsToHand(state, humanId, TestCards.Shield, TestCards.Shotgun);

        var result = Infect(state, zombieId, humanId);

        var hand = state.PlayerHands.First(h => h.UserId == humanId);
        Assert.Equal(2, hand.InventoryCount());
        Assert.Equal(TestCards.Infection.Id, hand.RoleCardId);
        Assert.DoesNotContain(TestCards.Shield.Id, hand.DisabledCardIds);
        Assert.Contains(TestCards.Shotgun.Id, hand.DisabledCardIds);
        Assert.NotNull(result.InfectionTransform);
        Assert.True(result.InfectionTransform!.HasRemainingShieldCard);
        Assert.Equal(1, result.InfectionTransform.DisabledShotguns);
        Assert.Equal(0, result.InfectionTransform.DisabledHeals);
    }

    private static GameSessionState CreateDayState(Guid humanId, Guid zombieId)
    {
        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));
        state.CurrentPhase = GamePhase.Day;
        GameTestBuilder.ResetActionPoints(state);
        return state;
    }

    private CardEffectResult Infect(GameSessionState state, Guid zombieId, Guid humanId) =>
        _resolver.Play(new CardEffectContext
        {
            State = state,
            ActorUserId = zombieId,
            TargetUserId = humanId,
            Card = TestCards.Infection
        });
}
