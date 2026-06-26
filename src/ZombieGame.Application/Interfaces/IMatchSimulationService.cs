namespace ZombieGame.Application.Interfaces;

using ZombieGame.Application.Simulation;

public interface IMatchSimulationService
{
    Task<MatchSimulationResult> RunAsync(MatchSimulationOptions? options = null, CancellationToken cancellationToken = default);
    Task<PassPenaltyComparisonResult> RunPassPenaltyComparisonAsync(MatchSimulationOptions? options = null, CancellationToken cancellationToken = default);
    Task<MaxPassLimitComparisonResult> RunMaxPassLimitComparisonAsync(MatchSimulationOptions? options = null, CancellationToken cancellationToken = default);
    Task<SuspicionVotingComparisonResult> RunSuspicionVotingComparisonAsync(MatchSimulationOptions? options = null, CancellationToken cancellationToken = default);
    Task<PowerZombieShieldBlockComparisonResult> RunPowerZombieShieldBlockComparisonAsync(MatchSimulationOptions? options = null, CancellationToken cancellationToken = default);
}
