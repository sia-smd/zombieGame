namespace ZombieGame.Application.Tests.Bots;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Bots;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Options;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public class BotPassTests
{
    [Fact]
    public async Task BotCanPass_WhenNoValidTarget()
    {
        var botId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((botId, PlayerRole.Human, true));
        state.Players[0].ActionsPerTurn = 2;
        state.Players[0].ActionsUsedThisTurn = 0;
        state.PlayerHands.First(h => h.UserId == botId).InventorySlot1 = TestCards.Shotgun.Id;

        var store = new FakeSessionStore(state);
        var botService = new BotService(
            new FakeMatchRepository(),
            new FakeUserRepository(),
            new FakeUnitOfWork(),
            new FakeCardRegistry(),
            store,
            new CardPlayValidator(),
            Options.Create(new GameSettings { ActionsPerTurn = 2 }));

        var action = await botService.DecideNextActionAsync(state.MatchId, botId);

        Assert.NotNull(action);
        Assert.Equal("Pass", action!.ActionType);
    }

    private sealed class FakeSessionStore(GameSessionState state) : IGameSessionStore
    {
        public Task<GameSessionState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<GameSessionState?>(state.MatchId == matchId ? state : null);

        public Task<GameSessionState?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<GameSessionState?>(null);

        public Task SetAsync(GameSessionState s, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryAddAsync(Guid matchId, GameSessionState s, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeMatchRepository : IMatchRepository
    {
        public Task<Domain.Entities.Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Entities.Match?>(null);
        public Task<Domain.Entities.Match?> GetBySessionTokenAsync(string sessionToken, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Entities.Match?>(null);
        public Task<Domain.Entities.Match?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Entities.Match?>(null);
        public Task<IReadOnlyList<Domain.Entities.Match>> GetActiveMatchesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Domain.Entities.Match>>(Array.Empty<Domain.Entities.Match>());
        public Task<IReadOnlyList<Domain.Entities.Match>> GetWaitingRoomsNeedingBotFillAsync(TimeSpan minAge, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Domain.Entities.Match>>(Array.Empty<Domain.Entities.Match>());
        public Task<IReadOnlyList<Domain.Entities.Match>> GetOpenWaitingRoomsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Domain.Entities.Match>>(Array.Empty<Domain.Entities.Match>());
        public Task AddAsync(Domain.Entities.Match match, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddPlayerAsync(Domain.Entities.MatchPlayer player, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RemovePlayer(Domain.Entities.MatchPlayer player) { }
        public void Update(Domain.Entities.Match match) { }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<Domain.Entities.User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Entities.User?>(null);
        public Task<Domain.Entities.User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Entities.User?>(null);
        public Task<Domain.Entities.User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Entities.User?>(null);
        public Task<Domain.Entities.User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Entities.User?>(null);
        public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Domain.Entities.User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Domain.Entities.User user) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeCardRegistry : ICardRegistry
    {
        public IReadOnlyList<Domain.Entities.CardDefinition> GetAll() => [TestCards.Shotgun];
        public Domain.Entities.CardDefinition? GetById(Guid id) => TestCards.Shotgun;
        public IReadOnlyList<Domain.Entities.CardDefinition> GetByType(CardType type) => [];
    }
}
