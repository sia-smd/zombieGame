namespace ZombieGame.Application.Tests.Account;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Account;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Services;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public class AccountRecoveryServiceTests
{
    [Fact]
    public async Task SendOtp_RejectsWrongPasswordWithGenericError()
    {
        var fixture = CreateFixture();
        fixture.Users.Users.Add(Guest());
        fixture.Users.Users.Add(MobileAccount());

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            fixture.Service.SendRecoverAccountOtpAsync(
                fixture.GuestId,
                new RecoverAccountSendRequest("Survivor", "wrong"),
                "127.0.0.1"));

        Assert.Equal(AccountRecoveryService.RecoverFailedMessage, ex.Message);
        Assert.Empty(fixture.Sms.Sent);
        Assert.Empty(fixture.Challenges.Items);
    }

    [Fact]
    public async Task SendOtp_SendsSmsAndStoresChallengeBoundToGuestAndTarget()
    {
        var fixture = CreateFixture();
        fixture.Users.Users.Add(Guest());
        fixture.Users.Users.Add(MobileAccount());

        var response = await fixture.Service.SendRecoverAccountOtpAsync(
            fixture.GuestId,
            new RecoverAccountSendRequest("Survivor", "secret"),
            "127.0.0.1");

        Assert.True(response.Success);
        Assert.Single(fixture.Sms.Sent);
        Assert.Equal("+989121234567", fixture.Sms.Sent[0].Mobile);
        Assert.Equal(6, fixture.Sms.Sent[0].Code.Length);
        Assert.Single(fixture.Challenges.Items);
        Assert.Equal(fixture.GuestId, fixture.Challenges.Items[0].GuestPlayerId);
        Assert.Equal(fixture.TargetId, fixture.Challenges.Items[0].TargetPlayerId);
        Assert.Equal(AccountRecoveryPurpose.RecoverAccount, fixture.Challenges.Items[0].Purpose);
    }

    [Fact]
    public async Task SendOtp_RequiresGuestCaller()
    {
        var fixture = CreateFixture();
        fixture.Users.Users.Add(MobileAccount());

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            fixture.Service.SendRecoverAccountOtpAsync(
                fixture.TargetId,
                new RecoverAccountSendRequest("Survivor", "secret"),
                "127.0.0.1"));

        Assert.Equal(AccountRecoveryService.GuestRequiredMessage, ex.Message);
    }

    [Fact]
    public async Task VerifyOtp_IssuesTargetSessionAndDeactivatesGuestSession()
    {
        var fixture = CreateFixture();
        fixture.Users.Users.Add(Guest());
        fixture.Users.Users.Add(MobileAccount());
        var guestSessionId = Guid.NewGuid();
        fixture.Sessions.Sessions.Add(new PlayerSession
        {
            Id = guestSessionId,
            PlayerId = fixture.GuestId,
            IsActive = true,
            ExpireDate = DateTime.UtcNow.AddDays(1),
            AccessTokenJti = "guest-jti",
            RefreshTokenHash = "guest-refresh"
        });

        await fixture.Service.SendRecoverAccountOtpAsync(
            fixture.GuestId,
            new RecoverAccountSendRequest("Survivor", "secret"),
            "127.0.0.1");
        var code = fixture.Sms.Sent[0].Code;

        var response = await fixture.Service.VerifyRecoverAccountOtpAsync(
            fixture.GuestId,
            guestSessionId,
            new RecoverAccountVerifyRequest(
                "Survivor",
                "secret",
                code,
                "device-web",
                DevicePlatform.Android,
                "1.0.0"),
            "127.0.0.1");

        Assert.Equal(fixture.TargetId, response.PlayerId);
        Assert.Equal("Survivor", response.Username);
        Assert.Equal("access-token", response.AccessToken);
        Assert.False(fixture.Sessions.Sessions.Single(s => s.Id == guestSessionId).IsActive);
        Assert.Contains(fixture.Sessions.Sessions, s => s.PlayerId == fixture.TargetId && s.IsActive);
        Assert.NotNull(fixture.Challenges.Items[0].ConsumedAt);
    }

    [Fact]
    public async Task VerifyOtp_RejectsCodeFromDifferentGuest()
    {
        var fixture = CreateFixture();
        var otherGuestId = Guid.NewGuid();
        fixture.Users.Users.Add(Guest());
        fixture.Users.Users.Add(new User
        {
            Id = otherGuestId,
            Username = "GuestOther",
            AccountType = AccountType.Guest,
            PasswordHash = "x"
        });
        fixture.Users.Users.Add(MobileAccount());

        await fixture.Service.SendRecoverAccountOtpAsync(
            fixture.GuestId,
            new RecoverAccountSendRequest("Survivor", "secret"),
            "127.0.0.1");
        var code = fixture.Sms.Sent[0].Code;

        await Assert.ThrowsAsync<ServiceException>(() =>
            fixture.Service.VerifyRecoverAccountOtpAsync(
                otherGuestId,
                null,
                new RecoverAccountVerifyRequest(
                    "Survivor",
                    "secret",
                    code,
                    "device-web",
                    DevicePlatform.Android,
                    "1.0.0"),
                "127.0.0.1"));
    }

    private Fixture CreateFixture()
    {
        var users = new InMemoryUserRepository();
        var sessions = new InMemorySessionRepository();
        var devices = new InMemoryDeviceRepository();
        var logs = new InMemoryLoginLogRepository();
        var challenges = new InMemoryChallengeRepository();
        var sms = new CapturingSmsService();
        var service = new AccountRecoveryService(
            users,
            new InMemoryProfileRepository(),
            sessions,
            devices,
            logs,
            challenges,
            new FakeUnitOfWork(),
            new FakeTokenService(),
            sms,
            new FakePasswordHasher(),
            Options.Create(new AccountSettings()),
            NullLogger<AccountRecoveryService>.Instance);

        return new Fixture(service, users, sessions, challenges, sms);
    }

    private User Guest() => new()
    {
        Id = Fixture.DefaultGuestId,
        Username = "Guest111",
        AccountType = AccountType.Guest,
        PasswordHash = "guest"
    };

    private User MobileAccount() => new()
    {
        Id = Fixture.DefaultTargetId,
        Username = "Survivor",
        PhoneNumber = "+989121234567",
        PasswordHash = "secret",
        AccountType = AccountType.Mobile,
        MobileVerified = true,
        Coins = 40,
        Profile = new PlayerProfile
        {
            PlayerId = Fixture.DefaultTargetId,
            Name = "Survivor",
            ImageId = "avatar_survivor_01",
            Level = 4
        }
    };

    private sealed record Fixture(
        AccountRecoveryService Service,
        InMemoryUserRepository Users,
        InMemorySessionRepository Sessions,
        InMemoryChallengeRepository Challenges,
        CapturingSmsService Sms)
    {
        public static readonly Guid DefaultGuestId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid DefaultTargetId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public Guid GuestId => DefaultGuestId;
        public Guid TargetId => DefaultTargetId;
    }

    private sealed class CapturingSmsService : ISmsService
    {
        public List<(string Mobile, string Code)> Sent { get; } = new();

        public Task SendVerificationCodeAsync(string mobileNumber, string code, CancellationToken cancellationToken = default)
        {
            Sent.Add((mobileNumber, code));
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryChallengeRepository : IAccountRecoveryChallengeRepository
    {
        public List<AccountRecoveryChallenge> Items { get; } = new();

        public Task AddAsync(AccountRecoveryChallenge challenge, CancellationToken cancellationToken = default)
        {
            Items.Add(challenge);
            return Task.CompletedTask;
        }

        public Task<AccountRecoveryChallenge?> GetActiveAsync(
            Guid guestPlayerId,
            Guid targetPlayerId,
            AccountRecoveryPurpose purpose,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items
                .Where(c =>
                    c.GuestPlayerId == guestPlayerId
                    && c.TargetPlayerId == targetPlayerId
                    && c.Purpose == purpose
                    && c.ConsumedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefault());

        public Task InvalidateActiveAsync(
            Guid guestPlayerId,
            Guid targetPlayerId,
            AccountRecoveryPurpose purpose,
            CancellationToken cancellationToken = default)
        {
            foreach (var challenge in Items.Where(c =>
                c.GuestPlayerId == guestPlayerId
                && c.TargetPlayerId == targetPlayerId
                && c.Purpose == purpose
                && c.ConsumedAt == null))
            {
                challenge.ConsumedAt = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        public void Update(AccountRecoveryChallenge challenge) { }
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
        public Task<PlayerProfile?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PlayerProfile?>(null);

        public Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;

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
        public Task AddAsync(PlayerLoginLog log, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
