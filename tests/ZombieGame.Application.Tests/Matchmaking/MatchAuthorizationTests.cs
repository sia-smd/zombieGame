namespace ZombieGame.Application.Tests.Matchmaking;

using ZombieGame.Application.Services;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public class MatchAuthorizationTests
{
    [Fact]
    public async Task GetMatch_HidesMatchFromNonMembers()
    {
        var memberId = Guid.NewGuid();
        var match = CreateMatch(memberId, MatchStatus.InProgress);
        var service = new MatchService(new FakeMatchRepository(match), new StubUserRepository(), new NullSummaryStore());
        Assert.NotNull(await service.GetMatchAsync(memberId, match.Id));
        Assert.Null(await service.GetMatchAsync(Guid.NewGuid(), match.Id));
    }

    [Fact]
    public async Task GetMatchPlayers_HidesOtherRolesUntilMatchIsFinished()
    {
        var requesterId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.InProgress,
            Players =
            [
                new MatchPlayer { UserId = requesterId, SeatIndex = 0, Role = PlayerRole.Human, IsAlive = true },
                new MatchPlayer { UserId = opponentId, SeatIndex = 1, Role = PlayerRole.Zombie, IsAlive = true }
            ]
        };
        var users = new StubUserRepository();
        users.Users[requesterId] = new User { Id = requesterId, Username = "Me" };
        users.Users[opponentId] = new User { Id = opponentId, Username = "Them" };
        var service = new MatchService(new FakeMatchRepository(match), users, new NullSummaryStore());

        var live = await service.GetMatchPlayersAsync(requesterId, match.Id);
        Assert.NotNull(live);
        Assert.Equal(PlayerRole.Human, live!.Single(p => p.UserId == requesterId).Role);
        Assert.Equal(PlayerRole.Unknown, live.Single(p => p.UserId == opponentId).Role);

        match.Status = MatchStatus.Finished;
        match.WinningTeam = WinTeam.Humans;
        match.TotalDays = 4;
        var finished = await service.GetMatchPlayersAsync(requesterId, match.Id);
        Assert.Equal(PlayerRole.Zombie, finished!.Single(p => p.UserId == opponentId).Role);

        var report = await service.GetMatchResultAsync(requesterId, match.Id);
        Assert.NotNull(report);
        Assert.Equal(4, report!.TotalDays);
        Assert.Equal(WinTeam.Humans, report.WinningTeam);
        Assert.Null(await service.GetMatchPlayersAsync(Guid.NewGuid(), match.Id));
    }

    private static Match CreateMatch(Guid memberId, MatchStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            Status = status,
            SessionToken = "secret-session",
            Players = [new MatchPlayer { UserId = memberId, Role = PlayerRole.Human }]
        };

    private sealed class NullSummaryStore : IMatchSummaryStore
    {
        public Task SaveAsync(MatchSummary summary, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<MatchSummary?> GetAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MatchSummary?>(null);
    }

    private sealed class StubUserRepository : IUserRepository
    {
        public Dictionary<Guid, User> Users { get; } = new();

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.GetValueOrDefault(id));

        public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Values.FirstOrDefault(u => u.Username == username));

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Update(User user) { }
    }
}
