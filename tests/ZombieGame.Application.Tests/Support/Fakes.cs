namespace ZombieGame.Application.Tests.Support;

using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Interfaces;

public sealed class FakeCoinService : ICoinService
{
    public List<Guid> Winners { get; } = new();
    public List<Guid> Losers { get; } = new();

    public Task<bool> CanAffordEntryFeeAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task DeductEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task AwardWinRewardAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default)
    {
        Winners.Add(userId);
        return Task.CompletedTask;
    }

    public Task RecordLossAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Losers.Add(userId);
        return Task.CompletedTask;
    }

    public Task RefundEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class FakeMatchRepository(Match match) : IMatchRepository
{
    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Match?>(match.Id == id ? match : null);

    public Task<Match?> GetBySessionTokenAsync(string sessionToken, CancellationToken cancellationToken = default) =>
        Task.FromResult<Match?>(match);

    public Task<Match?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Match?>(match);

    public Task<IReadOnlyList<Match>> GetActiveMatchesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Match>>(new[] { match });

    public Task AddAsync(Match m, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Update(Match updated) { }
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}

public sealed class FakeCardRegistry : ICardRegistry
{
    public IReadOnlyList<CardDefinition> GetAll() =>
    [
        TestCards.Shotgun, TestCards.Heal, TestCards.Shield,
        TestCards.Infection, TestCards.PowerInfection
    ];

    public CardDefinition? GetById(Guid id) => GetAll().FirstOrDefault(c => c.Id == id);

    public IReadOnlyList<CardDefinition> GetByType(Domain.Enums.CardType type) =>
        GetAll().Where(c => c.Type == type).ToList();
}
