namespace ZombieGame.Api.Services;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;

public sealed class GameLoopHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GameSettings _settings;
    private readonly ILogger<GameLoopHostedService> _logger;

    public GameLoopHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<GameSettings> settings,
        ILogger<GameLoopHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.EnableGameLoop)
        {
            _logger.LogInformation("Game loop is disabled.");
            return;
        }

        _logger.LogInformation("Game loop started with interval {IntervalMs}ms", _settings.GameLoopIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IGameLoopProcessor>();
                await processor.ProcessActiveMatchesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in game loop iteration");
            }

            await Task.Delay(_settings.GameLoopIntervalMs, stoppingToken);
        }
    }
}
