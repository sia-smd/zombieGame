namespace ZombieGame.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ZombieGame.Application.DTOs.Client;
using ZombieGame.Application.Options;

[ApiController]
[Route("api/client")]
[AllowAnonymous]
public class ClientController : ControllerBase
{
    private readonly HealthCheckService _healthChecks;
    private readonly ClientSettings _client;

    public ClientController(HealthCheckService healthChecks, IOptions<ClientSettings> client)
    {
        _healthChecks = healthChecks;
        _client = client.Value;
    }

    [HttpGet("boot")]
    public async Task<ActionResult<ClientBootResponse>> Boot(CancellationToken cancellationToken)
    {
        var report = await _healthChecks.CheckHealthAsync(cancellationToken);
        var database = IsHealthy(report, "sql");
        var redisEnabled = report.Entries.ContainsKey("redis");
        var redis = redisEnabled ? IsHealthy(report, "redis") : (bool?)null;

        return Ok(new ClientBootResponse(
            _client.ServerVersion,
            _client.MinClientVersion,
            report.Status != HealthStatus.Unhealthy,
            database,
            redis));
    }

    private static bool IsHealthy(HealthReport report, string name) =>
        report.Entries.TryGetValue(name, out var entry) && entry.Status == HealthStatus.Healthy;
}
