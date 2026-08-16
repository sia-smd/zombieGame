namespace ZombieGame.Application.Tests.Account;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZombieGame.Application.DTOs.Account;
using ZombieGame.Application.DTOs.Profile;
using ZombieGame.Application.Options;
using ZombieGame.Application.Services;
using ZombieGame.Application.Validation;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public class AccountServiceTests
{
    private readonly AccountSettings _accountSettings = new();
    private readonly GameSettings _gameSettings = new() { StartingCoins = 10 };

    [Fact]
    public async Task RegisterGuest_CreatesPlayerProfileDeviceAndTokens()
    {
        var users = new InMemoryUserRepository();
        var profiles = new InMemoryProfileRepository();
        var sessions = new InMemorySessionRepository();
        var devices = new InMemoryDeviceRepository();
        var logs = new InMemoryLoginLogRepository();
        var tokenService = new FakeTokenService();
        var service = CreateService(users, profiles, sessions, devices, logs, tokenService);

        var response = await service.RegisterGuestAsync(
            new RegisterGuestRequest("device-abc", DevicePlatform.Android, "1.0.0"),
            "127.0.0.1");

        Assert.NotEqual(Guid.Empty, response.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.Equal(1, response.Profile.Level);
        Assert.Equal("avatar_default_01", response.Profile.ImageId);
        Assert.StartsWith("Guest", response.Profile.Name);
        Assert.Single(users.Users);
        Assert.Equal(AccountType.Guest, users.Users[0].AccountType);
        Assert.Single(profiles.Profiles);
        Assert.Single(devices.Devices);
        Assert.Single(sessions.Sessions);
        Assert.Single(logs.Logs);
    }

    [Fact]
    public async Task AddMobile_SetsPendingVerification()
    {
        var users = new InMemoryUserRepository();
        var profiles = new InMemoryProfileRepository();
        var playerId = Guid.NewGuid();
        users.Users.Add(new User { Id = playerId, Username = "Guest123", AccountType = AccountType.Guest });
        profiles.Profiles.Add(new PlayerProfile { PlayerId = playerId, Name = "Guest123", ImageId = "avatar_default_01" });

        var service = CreateService(
            users,
            profiles,
            new InMemorySessionRepository(),
            new InMemoryDeviceRepository(),
            new InMemoryLoginLogRepository(),
            new FakeTokenService());

        var response = await service.AddMobileAsync(playerId, new AddMobileRequest("+989121234567"));

        Assert.True(response.Success);
        Assert.True(response.VerificationRequired);
        Assert.Equal("09121234567", users.Users[0].PendingPhoneNumber);
        Assert.False(users.Users[0].MobileVerified);
    }

    [Fact]
    public async Task VerifyMobile_AcceptsMasterOtp1234()
    {
        var users = new InMemoryUserRepository();
        var profiles = new InMemoryProfileRepository();
        var playerId = Guid.NewGuid();
        users.Users.Add(new User { Id = playerId, Username = "Guest123", AccountType = AccountType.Guest });
        profiles.Profiles.Add(new PlayerProfile { PlayerId = playerId, Name = "Guest123", ImageId = "avatar_default_01" });

        var service = CreateService(
            users,
            profiles,
            new InMemorySessionRepository(),
            new InMemoryDeviceRepository(),
            new InMemoryLoginLogRepository(),
            new FakeTokenService());

        await service.AddMobileAsync(playerId, new AddMobileRequest("09354214334"));
        var response = await service.VerifyMobileAsync(
            playerId,
            new VerifyMobileRequest("09354214334", "1234"));

        Assert.True(response.Success);
        Assert.Equal("09354214334", users.Users[0].PhoneNumber);
        Assert.True(users.Users[0].MobileVerified);
        Assert.Equal(AccountType.Mobile, users.Users[0].AccountType);
    }

    [Fact]
    public async Task Logout_DeactivatesSessionOnly()
    {
        var users = new InMemoryUserRepository();
        var sessions = new InMemorySessionRepository();
        var playerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        users.Users.Add(new User { Id = playerId, Username = "Guest123" });
        sessions.Sessions.Add(new PlayerSession
        {
            Id = sessionId,
            PlayerId = playerId,
            IsActive = true,
            ExpireDate = DateTime.UtcNow.AddDays(1)
        });

        var service = CreateService(
            users,
            new InMemoryProfileRepository(),
            sessions,
            new InMemoryDeviceRepository(),
            new InMemoryLoginLogRepository(),
            new FakeTokenService());

        var response = await service.LogoutAsync(playerId, sessionId, "127.0.0.1", "device-1");

        Assert.True(response.Success);
        Assert.False(sessions.Sessions[0].IsActive);
        Assert.Single(users.Users);
    }

    [Fact]
    public void ProfileNameValidator_RejectsForbiddenWords()
    {
        var validator = new ProfileNameValidator(new ForbiddenWordsService(), 20);
        Assert.Throws<Application.Common.ServiceException>(() => validator.Validate("bad admin"));
    }

    private AccountService CreateService(
        InMemoryUserRepository users,
        InMemoryProfileRepository profiles,
        InMemorySessionRepository sessions,
        InMemoryDeviceRepository devices,
        InMemoryLoginLogRepository logs,
        ITokenService tokenService) =>
        new(
            users,
            profiles,
            sessions,
            devices,
            logs,
            new FakeUnitOfWork(),
            tokenService,
            new MockSmsService(NullLogger<MockSmsService>.Instance),
            new FakePasswordHasher(),
            new ForbiddenWordsService(),
            Options.Create(_gameSettings),
            Options.Create(_accountSettings),
            NullLogger<AccountService>.Instance);

    [Fact]
    public async Task Login_CreatesSessionForVerifiedMobileAccount()
    {
        var users = new InMemoryUserRepository();
        var sessions = new InMemorySessionRepository();
        var devices = new InMemoryDeviceRepository();
        var logs = new InMemoryLoginLogRepository();
        var playerId = Guid.NewGuid();
        users.Users.Add(new User
        {
            Id = playerId,
            Username = "Survivor",
            PhoneNumber = "+989121234567",
            PasswordHash = "secret",
            AccountType = AccountType.Mobile,
            MobileVerified = true,
            Coins = 12,
            Profile = new PlayerProfile
            {
                PlayerId = playerId,
                Name = "Survivor",
                ImageId = "avatar_survivor_01",
                Level = 3
            }
        });

        var service = CreateService(
            users,
            new InMemoryProfileRepository(),
            sessions,
            devices,
            logs,
            new FakeTokenService());

        var response = await service.LoginAsync(
            new AccountLoginRequest(
                "+989121234567",
                "secret",
                "device-login",
                DevicePlatform.Android,
                "1.0.0"),
            "127.0.0.1");

        Assert.Equal(playerId, response.PlayerId);
        Assert.Equal("Survivor", response.Username);
        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("refresh-token", response.RefreshToken);
        Assert.Equal(3, response.Profile.Level);
        Assert.Single(sessions.Sessions);
        Assert.True(sessions.Sessions[0].IsActive);
        Assert.Single(devices.Devices);
        Assert.Contains(logs.Logs, log => log.Action == "login");
    }

    [Fact]
    public async Task Login_RejectsUnverifiedOrWrongPassword()
    {
        var users = new InMemoryUserRepository();
        var playerId = Guid.NewGuid();
        users.Users.Add(new User
        {
            Id = playerId,
            Username = "Guest123",
            PhoneNumber = "+989121234567",
            PasswordHash = "secret",
            AccountType = AccountType.Guest,
            MobileVerified = false
        });

        var service = CreateService(
            users,
            new InMemoryProfileRepository(),
            new InMemorySessionRepository(),
            new InMemoryDeviceRepository(),
            new InMemoryLoginLogRepository(),
            new FakeTokenService());

        await Assert.ThrowsAsync<Application.Common.ServiceException>(() =>
            service.LoginAsync(
                new AccountLoginRequest(
                    "+989121234567",
                    "secret",
                    "device-login",
                    DevicePlatform.Android,
                    "1.0.0"),
                "127.0.0.1"));
    }

    private sealed class FakeTokenService : ITokenService
    {
        public TokenPairResult CreateTokenPair(Guid userId, string username, Guid sessionId) =>
            new("access-token", Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(30));

        public string GenerateRefreshToken() => "refresh-token";

        public string HashToken(string token) => token;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string hash) => password == hash;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = new();

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Username == username));

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.PhoneNumber == phoneNumber));

        public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(u => u.Username == username));

        public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(u => u.PhoneNumber == phoneNumber));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public void Update(User user) { }
    }

    private sealed class InMemoryProfileRepository : IPlayerProfileRepository
    {
        public List<PlayerProfile> Profiles { get; } = new();

        public Task<PlayerProfile?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Profiles.FirstOrDefault(p => p.PlayerId == playerId));

        public Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken = default)
        {
            Profiles.Add(profile);
            return Task.CompletedTask;
        }

        public void Update(PlayerProfile profile) { }
    }

    private sealed class InMemorySessionRepository : IPlayerSessionRepository
    {
        public List<PlayerSession> Sessions { get; } = new();

        public Task<PlayerSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

        public Task<PlayerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.RefreshTokenHash == refreshTokenHash));

        public Task AddAsync(PlayerSession session, CancellationToken cancellationToken = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public void Update(PlayerSession session) { }
    }

    private sealed class InMemoryDeviceRepository : IPlayerDeviceRepository
    {
        public List<PlayerDevice> Devices { get; } = new();

        public Task<PlayerDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.Id == id));

        public Task<PlayerDevice?> GetByPlayerAndDeviceIdAsync(Guid playerId, string deviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.PlayerId == playerId && d.DeviceId == deviceId));

        public Task<IReadOnlyList<PlayerDevice>> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayerDevice>>(Devices.Where(d => d.PlayerId == playerId).ToList());

        public Task AddAsync(PlayerDevice device, CancellationToken cancellationToken = default)
        {
            Devices.Add(device);
            return Task.CompletedTask;
        }

        public void Update(PlayerDevice device) { }
    }

    private sealed class InMemoryLoginLogRepository : IPlayerLoginLogRepository
    {
        public List<PlayerLoginLog> Logs { get; } = new();

        public Task AddAsync(PlayerLoginLog log, CancellationToken cancellationToken = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }
    }
}
