namespace ZombieGame.Api.Services;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;

public sealed class ActiveMatchBootstrapService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActiveMatchBootstrapService> _logger;

    public ActiveMatchBootstrapService(
        IServiceScopeFactory scopeFactory,
        ILogger<ActiveMatchBootstrapService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var matchRepository = scope.ServiceProvider.GetRequiredService<IMatchRepository>();
        var activeMatches = scope.ServiceProvider.GetRequiredService<IActiveMatchRegistry>();
        var recovery = scope.ServiceProvider.GetRequiredService<ZombieGame.Application.Interfaces.IGameSessionRecoveryService>();

        var matches = await matchRepository.GetActiveMatchesAsync(cancellationToken);
        var inProgress = matches.Where(m => m.Status == MatchStatus.InProgress).ToList();

        foreach (var match in inProgress)
        {
            await recovery.TryRecoverAsync(match.Id, cancellationToken);
            await activeMatches.RegisterAsync(match.Id, cancellationToken);
        }

        if (inProgress.Count > 0)
            _logger.LogInformation("Bootstrapped {Count} in-progress matches into active game loop", inProgress.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
