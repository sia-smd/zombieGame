namespace ZombieGame.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;

public sealed class ForbiddenWordsService : IForbiddenWordsService
{
    private static readonly HashSet<string> Forbidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "moderator", "fuck", "shit", "asshole", "nazi"
    };

    public bool ContainsForbiddenWord(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(word => Forbidden.Contains(word));
    }
}

public sealed class AvatarCatalogService : IAvatarCatalogService
{
    private readonly HashSet<string> _allowed;

    public AvatarCatalogService(IOptions<AccountSettings> settings) =>
        _allowed = settings.Value.AllowedAvatarIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsAllowed(string imageId) => _allowed.Contains(imageId);

    public IReadOnlyList<string> GetAllowedAvatarIds() => _allowed.ToList();
}

public sealed class MockSmsService : ISmsService
{
    private readonly ILogger<MockSmsService> _logger;

    public MockSmsService(ILogger<MockSmsService> logger) => _logger = logger;

    public Task SendVerificationCodeAsync(string mobileNumber, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SMS verification code {Code} sent to {MobileNumber} (mock provider)", code, mobileNumber);
        return Task.CompletedTask;
    }
}

public sealed class AccountRecoveryService : IAccountRecoveryService
{
    public Task<bool> CanRecoverByMobileAsync(string mobileNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> CanLinkGoogleAccountAsync(string googleSubjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> CanLinkAppleAccountAsync(string appleSubjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
