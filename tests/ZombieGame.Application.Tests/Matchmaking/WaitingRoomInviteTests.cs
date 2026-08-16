namespace ZombieGame.Application.Tests.Matchmaking;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
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

public class WaitingRoomInviteTests
{
    [Fact]
    public async Task SendInvite_Fails_WhenTargetOffline()
    {
        var (service, hostId, target, match) = CreateScenario();

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            service.SendWaitingRoomInviteAsync(hostId, new SendRoomInviteRequest(match.Id, target.Username)));

        Assert.Equal("inviteErrors.userOffline", ex.Code);
    }

    [Fact]
    public async Task SendInvite_RequiresOnlineUser_ThenAcceptJoinsRoom()
    {
        var (service, hostId, target, match) = CreateScenario();
        var presence = new UserPresenceTracker();
        presence.AddConnection(target.Id, "conn-1");

        var wired = Recreate(presence, match, hostId, target);
        var sent = await wired.SendWaitingRoomInviteAsync(
            hostId,
            new SendRoomInviteRequest(match.Id, target.Username.ToUpperInvariant()));

        Assert.NotEqual(Guid.Empty, sent.InviteId);

        var joined = await wired.AcceptWaitingRoomInviteAsync(target.Id, sent.InviteId);
        Assert.Equal(match.Id, joined.MatchId);
        Assert.Contains(match.Players, p => p.UserId == target.Id);
    }

    [Fact]
    public async Task SendInvite_RejectsSelf_Bot_AndOffline()
    {
        var hostId = Guid.NewGuid();
        var match = WaitingMatch(hostId);
        var users = new DictionaryUserRepository();
        users.Add(hostId, "HostPlayer");
        var botId = Guid.NewGuid();
        users.Users[botId] = new User
        {
            Id = botId,
            Username = "Bot_abcd1234",
            PasswordHash = "BOT",
            CreatedAt = DateTime.UtcNow
        };

        var presence = new UserPresenceTracker();
        presence.AddConnection(hostId, "h");
        presence.AddConnection(botId, "b");

        var service = Build(match, users, presence);

        var self = await Assert.ThrowsAsync<ServiceException>(() =>
            service.SendWaitingRoomInviteAsync(hostId, new SendRoomInviteRequest(match.Id, "HostPlayer")));
        Assert.Equal("inviteErrors.self", self.Code);

        var bot = await Assert.ThrowsAsync<ServiceException>(() =>
            service.SendWaitingRoomInviteAsync(hostId, new SendRoomInviteRequest(match.Id, "Bot_abcd1234")));
        Assert.Equal("inviteErrors.bot", bot.Code);

        users.Add(Guid.NewGuid(), "OfflinePal");
        var offline = await Assert.ThrowsAsync<ServiceException>(() =>
            service.SendWaitingRoomInviteAsync(hostId, new SendRoomInviteRequest(match.Id, "OfflinePal")));
        Assert.Equal("inviteErrors.userOffline", offline.Code);
    }

    [Fact]
    public async Task DenyInvite_MarksRejected()
    {
        var hostId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var match = WaitingMatch(hostId);
        var users = new DictionaryUserRepository();
        users.Add(hostId, "host");
        users.Add(targetId, "guest_nick");
        var presence = new UserPresenceTracker();
        presence.AddConnection(targetId, "c");
        var invites = new RoomInviteStore();
        var service = Build(match, users, presence, invites);

        var sent = await service.SendWaitingRoomInviteAsync(
            hostId,
            new SendRoomInviteRequest(match.Id, "guest_nick"));

        await service.DenyWaitingRoomInviteAsync(targetId, sent.InviteId);
        Assert.Equal(RoomInviteStatus.Denied, invites.Get(sent.InviteId)!.Status);
        Assert.DoesNotContain(match.Players, p => p.UserId == targetId);
    }

    private static (MatchmakingService service, Guid hostId, User target, Match match) CreateScenario()
    {
        var hostId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var match = WaitingMatch(hostId);
        var users = new DictionaryUserRepository();
        users.Add(hostId, "host");
        users.Add(targetId, "friend");
        var service = Build(match, users, new UserPresenceTracker());
        return (service, hostId, users.Users[targetId], match);
    }

    private static MatchmakingService Recreate(
        UserPresenceTracker presence,
        Match match,
        Guid hostId,
        User target)
    {
        var users = new DictionaryUserRepository();
        users.Add(hostId, "host");
        users.Users[target.Id] = target;
        return Build(match, users, presence);
    }

    private static Match WaitingMatch(Guid hostId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.Waiting,
            SessionToken = "tok",
            Name = "Lobby",
            MaxPlayers = 4,
            CreatedAt = DateTime.UtcNow,
            Players =
            {
                new MatchPlayer { Id = Guid.NewGuid(), UserId = hostId, SeatIndex = 0, JoinedAt = DateTime.UtcNow }
            }
        };

    private static MatchmakingService Build(
        Match match,
        DictionaryUserRepository users,
        IUserPresenceTracker presence,
        IRoomInviteStore? invites = null) =>
        new(
            new MatchmakingQueue(),
            new MatchmakingPendingMatchStore(),
            new RecordingMatchRepository(match),
            users,
            new AlwaysAffordableCoinService(),
            new NoOpBotService(),
            new NoOpUnitOfWork(),
            new RecordingRoomStateMachine(),
            new FakePlayerActiveMatchStore(),
            Options.Create(new GameSettings { RoomInviteTtlSeconds = 60, MatchEntryFeeCoins = 0 }),
            NullLogger<MatchmakingService>.Instance,
            presence,
            invites ?? new RoomInviteStore());

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
                    .ToList());

        public void Update(Match match) { }
    }

    private sealed class RecordingRoomStateMachine : IRoomStateMachine
    {
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

        public Task SyncPlayersFromMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemovePlayerFromLobbyAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DiscardLobbyRoomAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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
        public Dictionary<Guid, User> Users { get; } = new();

        public void Add(Guid id, string username)
        {
            Users[id] = new User
            {
                Id = id,
                Username = username,
                PhoneNumber = id.ToString(),
                PasswordHash = "x",
                Coins = 50,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.TryGetValue(id, out var user) ? user : null);

        public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var normalized = username.Trim().ToLowerInvariant();
            return Task.FromResult(Users.Values.FirstOrDefault(u =>
                u.Username.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Values.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

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
