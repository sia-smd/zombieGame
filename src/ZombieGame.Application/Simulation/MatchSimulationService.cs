namespace ZombieGame.Application.Simulation;

using Microsoft.Extensions.Options;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;

public sealed class MatchSimulationService : IMatchSimulationService
{
    private readonly DayEventOptions _dayEvents;
    private readonly GameSettings _gameSettings;

    public MatchSimulationService(
        IOptions<DayEventOptions>? dayEvents = null,
        IOptions<GameSettings>? gameSettings = null)
    {
        _dayEvents = dayEvents?.Value ?? new DayEventOptions();
        _gameSettings = gameSettings?.Value ?? new GameSettings();
    }

    public async Task<MatchSimulationResult> RunAsync(
        MatchSimulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MatchSimulationOptions();
        var started = DateTime.UtcNow;
        var aggregator = new BalanceStatisticsAggregator();
        var simulator = CreateSimulator(options);
        var parallelism = options.MaxParallelism <= 0
            ? Environment.ProcessorCount
            : options.MaxParallelism;

        if (parallelism <= 1 || options.MatchCount < parallelism)
        {
            var random = options.RandomSeed.HasValue ? new Random(options.RandomSeed.Value) : Random.Shared;
            for (var i = 0; i < options.MatchCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outcome = await simulator.RunSingleMatchAsync(options, random);
                aggregator.Add(outcome);
            }
        }
        else
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, options.MatchCount),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelism,
                    CancellationToken = cancellationToken
                },
                async (index, ct) =>
                {
                    var seed = options.RandomSeed.HasValue
                        ? options.RandomSeed.Value + index
                        : Random.Shared.Next();
                    var random = new Random(seed);
                    var outcome = await simulator.RunSingleMatchAsync(options, random);
                    aggregator.Add(outcome);
                });
        }

        var elapsed = DateTime.UtcNow - started;
        return new MatchSimulationResult
        {
            Options = options,
            Balance = aggregator.Build(options.MatchCount),
            PassTelemetry = aggregator.BuildPassTelemetry(options.MatchCount),
            InfectionTelemetry = aggregator.BuildInfectionTelemetry(options.MatchCount),
            VotingTelemetry = aggregator.BuildVotingTelemetry(options.MatchCount),
            Elapsed = elapsed,
            MatchesPerSecond = elapsed.TotalSeconds > 0
                ? Math.Round(options.MatchCount / elapsed.TotalSeconds, 2)
                : options.MatchCount
        };
    }

    public async Task<PassPenaltyComparisonResult> RunPassPenaltyComparisonAsync(
        MatchSimulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MatchSimulationOptions();
        var started = DateTime.UtcNow;

        var noPenaltyOptions = CloneOptions(options);
        noPenaltyOptions.PassPenaltyMode = false;

        var penaltyOptions = CloneOptions(options);
        penaltyOptions.PassPenaltyMode = true;

        var noPenalty = await RunAsync(noPenaltyOptions, cancellationToken);
        var passPenalty = await RunAsync(penaltyOptions, cancellationToken);

        return new PassPenaltyComparisonResult
        {
            Options = options,
            NoPenalty = noPenalty,
            PassPenaltyMode = passPenalty,
            TotalElapsed = DateTime.UtcNow - started
        };
    }

    public async Task<MaxPassLimitComparisonResult> RunMaxPassLimitComparisonAsync(
        MatchSimulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MatchSimulationOptions();
        var started = DateTime.UtcNow;

        var unlimitedOptions = CloneOptions(options);
        unlimitedOptions.MaxPassActionsPerDay = 0;

        var limitedOptions = CloneOptions(options);
        limitedOptions.MaxPassActionsPerDay = 1;

        var unlimited = await RunAsync(unlimitedOptions, cancellationToken);
        var limited = await RunAsync(limitedOptions, cancellationToken);

        return new MaxPassLimitComparisonResult
        {
            Options = options,
            UnlimitedPass = unlimited,
            MaxOnePassPerDay = limited,
            TotalElapsed = DateTime.UtcNow - started
        };
    }

    private static MatchSimulationOptions CloneOptions(MatchSimulationOptions source) =>
        new()
        {
            MatchCount = source.MatchCount,
            PlayerCount = source.PlayerCount,
            MaxTurnsPerMatch = source.MaxTurnsPerMatch,
            MaxCardPlaysPerBotPerDay = source.MaxCardPlaysPerBotPerDay,
            RandomSeed = source.RandomSeed,
            MaxParallelism = source.MaxParallelism,
            PassPenaltyMode = source.PassPenaltyMode,
            MaxPassActionsPerDay = source.MaxPassActionsPerDay,
            UseSuspicionBasedVoting = source.UseSuspicionBasedVoting,
            Scenario = source.Scenario
        };

    public async Task<SuspicionVotingComparisonResult> RunSuspicionVotingComparisonAsync(
        MatchSimulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MatchSimulationOptions();
        var started = DateTime.UtcNow;

        var randomOptions = CloneOptions(options);
        randomOptions.UseSuspicionBasedVoting = false;

        var suspicionOptions = CloneOptions(options);
        suspicionOptions.UseSuspicionBasedVoting = true;

        var randomVoting = await RunAsync(randomOptions, cancellationToken);
        var suspicionVoting = await RunAsync(suspicionOptions, cancellationToken);

        return new SuspicionVotingComparisonResult
        {
            Options = options,
            RandomVoting = randomVoting,
            SuspicionVoting = suspicionVoting,
            TotalElapsed = DateTime.UtcNow - started
        };
    }

    public Task<DetailedMatchReplay> RunDetailedReplayAsync(
        MatchSimulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MatchSimulationOptions { PlayerCount = 12 };
        options.MatchCount = 1;
        cancellationToken.ThrowIfCancellationRequested();

        var random = options.RandomSeed.HasValue ? new Random(options.RandomSeed.Value) : Random.Shared;
        var simulator = CreateSimulator(options);
        return simulator.RunDetailedReplayAsync(options, random);
    }

    private MatchSimulator CreateSimulator(MatchSimulationOptions options) =>
        new(options, _dayEvents, _gameSettings);
}
