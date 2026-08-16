namespace ZombieGame.Application.Tests.Account;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.Options;
using ZombieGame.Application.Services;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public class CoinServiceTests
{
    [Fact]
    public async Task CollectMatchEntryFees_DeductsOncePerHuman()
    {
        var human = NewUser(coins: 10);
        var users = new InMemoryUsers(human);
        var txs = new InMemoryTransactions();
        var service = Create(users, txs);

        var matchId = Guid.NewGuid();
        await service.CollectMatchEntryFeesAsync(matchId, [human.Id]);
        await service.CollectMatchEntryFeesAsync(matchId, [human.Id]);

        Assert.Equal(5, human.Coins);
        Assert.Single(txs.Items);
        Assert.Equal(TransactionType.MatchEntryFee, txs.Items[0].Type);
        Assert.Equal(-5, txs.Items[0].Amount);
    }

    [Fact]
    public async Task CollectMatchEntryFees_ThrowsIfAnyoneCannotAfford_WithoutCharging()
    {
        var rich = NewUser(coins: 10);
        var poor = NewUser(coins: 2);
        var users = new InMemoryUsers(rich, poor);
        var txs = new InMemoryTransactions();
        var service = Create(users, txs);

        await Assert.ThrowsAsync<ServiceException>(() =>
            service.CollectMatchEntryFeesAsync(Guid.NewGuid(), [rich.Id, poor.Id]));

        Assert.Equal(10, rich.Coins);
        Assert.Equal(2, poor.Coins);
        Assert.Empty(txs.Items);
    }

    private static CoinService Create(InMemoryUsers users, InMemoryTransactions txs) =>
        new(users, txs, new FakeUnitOfWork(), Options.Create(new GameSettings
        {
            MatchEntryFeeCoins = 5
        }));

    private static User NewUser(int coins) =>
        new()
        {
            Id = Guid.NewGuid(),
            Username = Guid.NewGuid().ToString("N")[..8],
            Coins = coins
        };

    private sealed class InMemoryUsers : IUserRepository
    {
        private readonly Dictionary<Guid, User> _users;

        public InMemoryUsers(params User[] users) =>
            _users = users.ToDictionary(u => u.Id);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.GetValueOrDefault(id));

        public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Update(User user) => _users[user.Id] = user;
    }

    private sealed class InMemoryTransactions : ITransactionRepository
    {
        public List<Transaction> Items { get; } = new();

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            Items.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Transaction>> GetByUserIdAsync(
            Guid userId,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>(Items.Where(t => t.UserId == userId).Take(limit).ToList());

        public Task<bool> ExistsAsync(
            Guid userId,
            Guid matchId,
            TransactionType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Any(t => t.UserId == userId && t.MatchId == matchId && t.Type == type));
    }
}
