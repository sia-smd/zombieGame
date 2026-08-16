namespace ZombieGame.Application.Tests.Matchmaking;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZombieGame.Application.DTOs.Matchmaking;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Application.Options;
using ZombieGame.Application.Services;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public class MatchmakingTimeoutTests
{
    [Fact]
    public async Task ProcessQueueTimeouts_CreatesMatchAndStagesPendingAssignment()
    {
        var userId = Guid.NewGuid();
        var queue = new MatchmakingQueue();
        queue.TryEnqueue(userId);

        var repository = new RecordingMatchRepository();
        var users = new DictionaryUserRepository(userId);
        var bots = new RecordingBotService();
        var settings = Options.Create(new GameSettings
        {
            MatchmakingPlayerCount = 2,
            MatchmakingBotFillTimeoutSeconds = 10,
            FillWithBotsWhenUnderCapacity = true
        });

        var service = new MatchmakingService(
            new TimedMatchmakingQueue(queue, DateTime.UtcNow.AddSeconds(-11)),
            new MatchmakingPendingMatchStore(),
            repository,
            users,
            new AlwaysAffordableCoinService(),
            bots,
            new NoOpUnitOfWork(),
            new NoOpRoomStateMachine(),
            new FakePlayerActiveMatchStore(),
            settings,
            NullLogger<MatchmakingService>.Instance);

        await service.ProcessQueueTimeoutsAsync();

        Assert.Single(repository.Matches);
        Assert.Equal(2, bots.FillCalls);

        var pending = await service.JoinQueueAsync(userId);
        Assert.False(pending.Queued);
        Assert.NotNull(pending.MatchId);
        Assert.False(string.IsNullOrWhiteSpace(pending.SessionToken));
    }

    private sealed class TimedMatchmakingQueue(IMatchmakingQueue inner, DateTime oldestUtc) : IMatchmakingQueue
    {
        public bool TryEnqueue(Guid userId) => inner.TryEnqueue(userId);
        public bool TryRemove(Guid userId) => inner.TryRemove(userId);
        public bool Contains(Guid userId) => inner.Contains(userId);
        public int Count => inner.Count;
        public bool TryDequeueBatch(int count, out List<Guid> userIds) => inner.TryDequeueBatch(count, out userIds);
        public void Requeue(IEnumerable<Guid> userIds) => inner.Requeue(userIds);
        public DateTime? GetOldestEnqueueUtc() => oldestUtc;
    }

    private sealed class RecordingMatchRepository : IMatchRepository
    {
        public List<Match> Matches { get; } = new();

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            Matches.Add(match);
            return Task.CompletedTask;
        }

        public Task AddPlayerAsync(MatchPlayer player, CancellationToken cancellationToken = default)
        {
            var match = Matches.FirstOrDefault(m => m.Id == player.MatchId);
            match?.Players.Add(player);
            return Task.CompletedTask;
        }

        public void RemovePlayer(MatchPlayer player)
        {
            foreach (var match in Matches)
            {
                if (match.Players.Remove(player))
                    return;
            }
        }

        public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Match?>(Matches.FirstOrDefault(m => m.Id == id));

        public Task<Match?> GetBySessionTokenAsync(string sessionToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<Match?>(Matches.FirstOrDefault(m => m.SessionToken == sessionToken));

        public Task<Match?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<Match>> GetActiveMatchesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Match>>(Matches);

        public Task<IReadOnlyList<Match>> GetWaitingRoomsNeedingBotFillAsync(
            TimeSpan minAge,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Match>>(Array.Empty<Match>());

        public Task<IReadOnlyList<Match>> GetOpenWaitingRoomsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Match>>(
                Matches
                    .Where(m =>
                        m.Status == MatchStatus.Waiting
                        && m.Players.Any(p => !p.IsBot)
                        && m.Players.Count < m.MaxPlayers)
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList());

        public void Update(Match match) { }
    }

    private sealed class NoOpRoomStateMachine : Application.Room.IRoomStateMachine
    {
        public Task<Domain.Models.Room.RoomState?> GetStateAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Domain.Models.Room.RoomState?>(null);

        public Task<Application.Room.RoomTransitionResult> DispatchAsync(
            Guid matchId,
            Application.Room.IRoomCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Application.Room.RoomTransitionResult.Stay());

        public Task<Application.Room.RoomTransitionResult> TickAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Application.Room.RoomTransitionResult.Stay());

        public Task<Domain.Models.Room.RoomState> InitializeRoomAsync(Match match, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Domain.Models.Room.RoomState { MatchId = match.Id });

        public Task SyncPlayersFromMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemovePlayerFromLobbyAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DiscardLobbyRoomAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Application.Room.RoomTransitionResult> StartGameAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Application.Room.RoomTransitionResult.Stay());

        public Task<Domain.Models.Room.RoomState?> SetPresenceAsync(
            Guid matchId,
            Guid userId,
            bool connected,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Domain.Models.Room.RoomState?>(null);
    }

    private sealed class DictionaryUserRepository : IUserRepository
    {
        public DictionaryUserRepository(Guid userId)
        {
            Users[userId] = new User
            {
                Id = userId,
                Username = "player",
                PhoneNumber = "1",
                PasswordHash = "x",
                Coins = 10,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Dictionary<Guid, User> Users { get; } = new();

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.TryGetValue(id, out var user) ? user : null);

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

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users[user.Id] = user;
            return Task.CompletedTask;
        }

        public void Update(User user) => Users[user.Id] = user;
    }

    private sealed class RecordingBotService : IBotService
    {
        public int FillCalls { get; private set; }

        public Task FillMatchWithBotsAsync(Guid matchId, int targetCount, CancellationToken cancellationToken = default)
        {
            FillCalls = targetCount;
            return Task.CompletedTask;
        }

        public Task<PendingBotAction?> DecideNextActionAsync(Guid matchId, Guid botUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PendingBotAction?>(null);

        public BotCardPlayDecision? TryDecideCardPlay(Domain.Models.GameSessionState state, Guid botUserId, Guid? battleOpponentId) => null;

        public Guid TryDecideVoteTarget(Domain.Models.GameSessionState state, Guid botUserId) =>
            state.AlivePlayers.First(p => p.UserId != botUserId).UserId;

        public bool ShouldSpeakInDiscussion(Domain.Models.GameSessionState state, Guid botUserId) => false;

        public StructuredDiscussionMessage? TryDecideDiscussionMessage(Domain.Models.GameSessionState state, Guid botUserId) => null;

        public void RecordDiscussionSpoke(Domain.Models.GameSessionState state, Guid botUserId) { }
    }

    private sealed class AlwaysAffordableCoinService : ICoinService
    {
        public Task<bool> CanAffordEntryFeeAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task DeductEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AwardWinRewardAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordLossAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefundEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
