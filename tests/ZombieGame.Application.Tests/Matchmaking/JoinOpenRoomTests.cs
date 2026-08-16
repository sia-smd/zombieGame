namespace ZombieGame.Application.Tests.Matchmaking;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZombieGame.Application.DTOs.Matchmaking;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Application.Services;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public class JoinOpenRoomTests
{
    [Fact]
    public async Task JoinOpenRoom_ByRoomCode_AddsPlayerAndReturnsSessionToken()
    {
        var hostId = Guid.NewGuid();
        var joinerId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.Parse("abcd1234-5678-4abc-8def-0123456789ab"),
            Status = MatchStatus.Waiting,
            CurrentPhase = GamePhase.Lobby,
            SessionToken = "shared-session-token",
            Name = "No Bots Room",
            MaxPlayers = 4,
            FillWithBots = false,
            CreatedAt = DateTime.UtcNow,
            Players =
            {
                new MatchPlayer
                {
                    Id = Guid.NewGuid(),
                    UserId = hostId,
                    SeatIndex = 0,
                    JoinedAt = DateTime.UtcNow,
                    User = new User
                    {
                        Id = hostId,
                        Username = "host",
                        PhoneNumber = "1",
                        PasswordHash = "x",
                        Coins = 50,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            }
        };

        var repository = new RecordingMatchRepository(match);
        var users = new DictionaryUserRepository(hostId, joinerId);
        var roomSync = new RecordingRoomStateMachine();
        var settings = Options.Create(new GameSettings
        {
            MinRoomPlayers = 2,
            MaxRoomPlayers = 12,
            MatchmakingPlayerCount = 4,
            MatchEntryFeeCoins = 5
        });

        var service = new MatchmakingService(
            new MatchmakingQueue(),
            new MatchmakingPendingMatchStore(),
            repository,
            users,
            new AlwaysAffordableCoinService(),
            new NoOpBotService(),
            new NoOpUnitOfWork(),
            roomSync,
            new FakePlayerActiveMatchStore(),
            settings,
            NullLogger<MatchmakingService>.Instance);

        var rooms = await service.ListOpenRoomsAsync();
        Assert.Single(rooms);
        Assert.Equal("ABCD", rooms[0].RoomCode);

        var joined = await service.JoinOpenRoomAsync(joinerId, new JoinOpenRoomRequest(RoomCode: "ABCD"));

        Assert.Equal(match.Id, joined.MatchId);
        Assert.Equal("shared-session-token", joined.SessionToken);
        Assert.Equal(2, match.Players.Count);
        Assert.Contains(match.Players, p => p.UserId == joinerId);
        Assert.Equal(1, roomSync.SyncCalls);
    }

    [Fact]
    public async Task JoinOpenRoom_Idempotent_WhenAlreadyInRoom()
    {
        var hostId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.Waiting,
            SessionToken = "tok",
            Name = "Room",
            MaxPlayers = 4,
            CreatedAt = DateTime.UtcNow,
            Players =
            {
                new MatchPlayer
                {
                    Id = Guid.NewGuid(),
                    UserId = hostId,
                    SeatIndex = 0,
                    JoinedAt = DateTime.UtcNow
                }
            }
        };

        var service = new MatchmakingService(
            new MatchmakingQueue(),
            new MatchmakingPendingMatchStore(),
            new RecordingMatchRepository(match),
            new DictionaryUserRepository(hostId),
            new AlwaysAffordableCoinService(),
            new NoOpBotService(),
            new NoOpUnitOfWork(),
            new RecordingRoomStateMachine(),
            new FakePlayerActiveMatchStore(),
            Options.Create(new GameSettings()),
            NullLogger<MatchmakingService>.Instance);

        var first = await service.JoinOpenRoomAsync(hostId, new JoinOpenRoomRequest(MatchId: match.Id));
        var second = await service.JoinOpenRoomAsync(hostId, new JoinOpenRoomRequest(MatchId: match.Id));

        Assert.Equal(first.SessionToken, second.SessionToken);
        Assert.Single(match.Players);
    }

    [Fact]
    public async Task LeaveWaitingRoom_RemovesJoiner_AndKeepsRoomListed()
    {
        var hostId = Guid.NewGuid();
        var joinerId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.Waiting,
            SessionToken = "tok",
            Name = "Room",
            MaxPlayers = 4,
            CreatedAt = DateTime.UtcNow,
            Players =
            {
                new MatchPlayer { Id = Guid.NewGuid(), UserId = hostId, SeatIndex = 0, JoinedAt = DateTime.UtcNow }
            }
        };

        var roomSync = new RecordingRoomStateMachine();
        var service = new MatchmakingService(
            new MatchmakingQueue(),
            new MatchmakingPendingMatchStore(),
            new RecordingMatchRepository(match),
            new DictionaryUserRepository(hostId, joinerId),
            new AlwaysAffordableCoinService(),
            new NoOpBotService(),
            new NoOpUnitOfWork(),
            roomSync,
            new FakePlayerActiveMatchStore(),
            Options.Create(new GameSettings()),
            NullLogger<MatchmakingService>.Instance);

        await service.JoinOpenRoomAsync(joinerId, new JoinOpenRoomRequest(MatchId: match.Id));
        await service.LeaveWaitingRoomAsync(joinerId, match.Id);

        Assert.DoesNotContain(match.Players, p => p.UserId == joinerId);
        Assert.Equal(MatchStatus.Waiting, match.Status);
        Assert.Equal(1, roomSync.RemoveCalls);
        Assert.Equal(0, roomSync.DiscardCalls);
        Assert.Single(await service.ListOpenRoomsAsync());
    }

    [Fact]
    public async Task LeaveWaitingRoom_LastHuman_FinishesAndDiscardsRoom()
    {
        var hostId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.Waiting,
            SessionToken = "tok",
            Name = "Solo",
            MaxPlayers = 4,
            CreatedAt = DateTime.UtcNow,
            Players =
            {
                new MatchPlayer { Id = Guid.NewGuid(), UserId = hostId, SeatIndex = 0, JoinedAt = DateTime.UtcNow },
                new MatchPlayer { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), SeatIndex = 1, IsBot = true, JoinedAt = DateTime.UtcNow }
            }
        };

        var roomSync = new RecordingRoomStateMachine();
        var service = new MatchmakingService(
            new MatchmakingQueue(),
            new MatchmakingPendingMatchStore(),
            new RecordingMatchRepository(match),
            new DictionaryUserRepository(hostId),
            new AlwaysAffordableCoinService(),
            new NoOpBotService(),
            new NoOpUnitOfWork(),
            roomSync,
            new FakePlayerActiveMatchStore(),
            Options.Create(new GameSettings()),
            NullLogger<MatchmakingService>.Instance);

        await service.LeaveWaitingRoomAsync(hostId, match.Id);

        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(1, roomSync.DiscardCalls);
        Assert.Empty(await service.ListOpenRoomsAsync());
    }

    private sealed class RecordingMatchRepository(Match seed) : IMatchRepository
    {
        public List<Match> Matches { get; } = [seed];

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            Matches.Add(match);
            return Task.CompletedTask;
        }

        public Task AddPlayerAsync(MatchPlayer player, CancellationToken cancellationToken = default)
        {
            var match = Matches.First(m => m.Id == player.MatchId);
            match.Players.Add(player);
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
            Task.FromResult(Matches.FirstOrDefault(m => m.Id == id));

        public Task<Match?> GetBySessionTokenAsync(string sessionToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(Matches.FirstOrDefault(m => m.SessionToken == sessionToken));

        public Task<Match?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<Match>> GetActiveMatchesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Match>>(Matches.Where(m => m.Status != MatchStatus.Finished).ToList());

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

    private sealed class RecordingRoomStateMachine : IRoomStateMachine
    {
        public int SyncCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public int DiscardCalls { get; private set; }

        public Task<RoomState?> GetStateAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RoomState?>(null);

        public Task<RoomTransitionResult> DispatchAsync(
            Guid matchId,
            IRoomCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RoomTransitionResult.Stay());

        public Task<RoomTransitionResult> TickAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RoomTransitionResult.Stay());

        public Task<RoomState> InitializeRoomAsync(Match match, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RoomState { MatchId = match.Id });

        public Task SyncPlayersFromMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
        {
            SyncCalls++;
            return Task.CompletedTask;
        }

        public Task RemovePlayerFromLobbyAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            return Task.CompletedTask;
        }

        public Task DiscardLobbyRoomAsync(Guid matchId, CancellationToken cancellationToken = default)
        {
            DiscardCalls++;
            return Task.CompletedTask;
        }

        public Task<RoomTransitionResult> StartGameAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RoomTransitionResult.Stay());

        public Task<RoomState?> SetPresenceAsync(
            Guid matchId,
            Guid userId,
            bool connected,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RoomState?>(null);
    }

    private sealed class DictionaryUserRepository : IUserRepository
    {
        public DictionaryUserRepository(params Guid[] userIds)
        {
            foreach (var id in userIds)
            {
                Users[id] = new User
                {
                    Id = id,
                    Username = $"user-{id.ToString()[..4]}",
                    PhoneNumber = id.ToString(),
                    PasswordHash = "x",
                    Coins = 50,
                    CreatedAt = DateTime.UtcNow
                };
            }
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

    private sealed class AlwaysAffordableCoinService : ICoinService
    {
        public Task<bool> CanAffordEntryFeeAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task DeductEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AwardWinRewardAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordLossAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RefundEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpBotService : IBotService
    {
        public Task FillMatchWithBotsAsync(Guid matchId, int targetCount, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PendingBotAction?> DecideNextActionAsync(
            Guid matchId,
            Guid botUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PendingBotAction?>(null);

        public BotCardPlayDecision? TryDecideCardPlay(
            GameSessionState state,
            Guid botUserId,
            Guid? battleOpponentId) => null;

        public Guid TryDecideVoteTarget(GameSessionState state, Guid botUserId) =>
            state.AlivePlayers.First(p => p.UserId != botUserId).UserId;

        public bool ShouldSpeakInDiscussion(GameSessionState state, Guid botUserId) => false;

        public StructuredDiscussionMessage? TryDecideDiscussionMessage(GameSessionState state, Guid botUserId) =>
            null;

        public void RecordDiscussionSpoke(GameSessionState state, Guid botUserId) { }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
