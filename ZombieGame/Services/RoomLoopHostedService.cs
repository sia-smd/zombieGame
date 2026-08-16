namespace ZombieGame.Api.Services;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;

public sealed class RoomLoopHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RoomSettings _settings;
    private readonly ILogger<RoomLoopHostedService> _logger;

    public RoomLoopHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RoomSettings> settings,
        ILogger<RoomLoopHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IRoomLoopProcessor>();
                await processor.ProcessActiveRoomsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Room loop iteration failed.");
            }

            await Task.Delay(_settings.RoomLoopIntervalMs, stoppingToken);
        }
    }
}
