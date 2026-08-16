namespace ZombieGame.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Simulation;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class SimulationController : ControllerBase
{
    private readonly IMatchSimulationService _simulationService;
    private readonly IWebHostEnvironment _environment;

    public SimulationController(IMatchSimulationService simulationService, IWebHostEnvironment environment)
    {
        _simulationService = simulationService;
        _environment = environment;
    }

    /// <summary>
    /// Runs automated bot-only match simulations and returns balance statistics.
    /// Available in Development only by default.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<MatchSimulationResult>> Run(
        [FromBody] MatchSimulationOptions? options,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        options ??= new MatchSimulationOptions();
        if (options.MatchCount is < 1 or > 100_000)
            return BadRequest(new { message = "MatchCount must be between 1 and 100000." });

        var result = await _simulationService.RunAsync(options, cancellationToken);
        return Ok(result);
    }

    [HttpPost("compare-pass-penalty")]
    public async Task<ActionResult<PassPenaltyComparisonResult>> ComparePassPenalty(
        [FromBody] MatchSimulationOptions? options,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        options ??= new MatchSimulationOptions();
        if (options.MatchCount is < 1 or > 100_000)
            return BadRequest(new { message = "MatchCount must be between 1 and 100000." });

        var result = await _simulationService.RunPassPenaltyComparisonAsync(options, cancellationToken);
        return Ok(result);
    }

    [HttpPost("compare-max-pass-limit")]
    public async Task<ActionResult<MaxPassLimitComparisonResult>> CompareMaxPassLimit(
        [FromBody] MatchSimulationOptions? options,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        options ??= new MatchSimulationOptions();
        if (options.MatchCount is < 1 or > 100_000)
            return BadRequest(new { message = "MatchCount must be between 1 and 100000." });

        var result = await _simulationService.RunMaxPassLimitComparisonAsync(options, cancellationToken);
        return Ok(result);
    }

    [HttpPost("compare-suspicion-voting")]
    public async Task<ActionResult<SuspicionVotingComparisonResult>> CompareSuspicionVoting(
        [FromBody] MatchSimulationOptions? options,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        options ??= new MatchSimulationOptions();
        if (options.MatchCount is < 1 or > 100_000)
            return BadRequest(new { message = "MatchCount must be between 1 and 100000." });

        var result = await _simulationService.RunSuspicionVotingComparisonAsync(options, cancellationToken);
        return Ok(result);
    }

    [HttpPost("replay")]
    public async Task<ActionResult<object>> RunDetailedReplay(
        [FromBody] MatchSimulationOptions? options,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        options ??= new MatchSimulationOptions { PlayerCount = 12, RandomSeed = 42 };
        options.MatchCount = 1;
        if (options.PlayerCount is < 2 or > 16)
            return BadRequest(new { message = "PlayerCount must be between 2 and 16." });

        var replay = await _simulationService.RunDetailedReplayAsync(options, cancellationToken);
        var text = MatchReplayTextFormatter.Format(replay);
        return Ok(new { replay, textReport = text });
    }
}
