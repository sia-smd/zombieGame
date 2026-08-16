namespace ZombieGame.Api.Services;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;

public sealed class MatchmakingTimeoutHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GameSettings _settings;
    private readonly ILogger<MatchmakingTimeoutHostedService> _logger;

    public MatchmakingTimeoutHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<GameSettings> settings,
        ILogger<MatchmakingTimeoutHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.MatchmakingBotFillTimeoutSeconds <= 0)
        {
            _logger.LogInformation("Matchmaking bot-fill timeout is disabled.");
            return;
        }

        _logger.LogInformation(
            "Matchmaking timeout worker started ({TimeoutSeconds}s bot fill).",
            _settings.MatchmakingBotFillTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var matchmaking = scope.ServiceProvider.GetRequiredService<IMatchmakingService>();
                await matchmaking.ProcessQueueTimeoutsAsync(stoppingToken);
                await matchmaking.ProcessWaitingRoomBotFillsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in matchmaking timeout worker");
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
