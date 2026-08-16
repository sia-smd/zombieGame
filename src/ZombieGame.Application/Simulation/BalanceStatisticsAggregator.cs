namespace ZombieGame.Application.Simulation;

using ZombieGame.Domain.Enums;

public sealed class BalanceStatisticsAggregator
{
    private readonly object _lock = new();
    private int _humanWins;
    private int _zombieWins;
    private int _stalemates;
    private long _totalTurns;
    private long _totalFriendlyFire;
    private long _totalVoteEliminations;
    private long _totalCardPlays;
    private long _totalHumanPasses;
    private long _totalZombiePasses;
    private long _totalPowerZombiePasses;
    private long _totalHumanCardPlays;
    private long _totalZombieCardPlays;
    private long _totalPowerZombieRoleCardPlays;
    private long _totalHumanPlayersAtStart;
    private long _totalZombiePlayersAtStart;
    private long _totalPowerZombiePlayersAtStart;
    private long _totalSuccessfulZombieInfections;
    private long _totalBlockedZombieInfections;
    private long _totalSuccessfulPowerZombieInfections;
    private long _totalZombieCures;
    private long _totalHumansConvertedToZombies;
    private long _totalZombiesConvertedToHumans;
    private long _totalHealCardPlays;
    private long _totalPowerZombieCardPlays;
    private long _totalBlockedPowerZombieInfections;
    private long _totalPowerZombieEliminations;
    private long _totalInfectedWithRemainingShield;
    private long _totalDisabledShotgunsAfterInfection;
    private long _totalDisabledHealsAfterInfection;
    private long _totalVotesCast;
    private long _totalVotesTargetingInfected;
    private long _totalZombieVoteEliminations;
    private long _totalPowerZombieVoteEliminations;
    private long _totalStartingZombies;
    private long _totalStartingPowerZombies;
    private double _sumSuspicionTowardHumans;
    private double _sumSuspicionTowardZombies;
    private double _sumSuspicionTowardPowerZombies;
    private long _suspicionSamplesTowardHumans;
    private long _suspicionSamplesTowardZombies;
    private long _suspicionSamplesTowardPowerZombies;
    private readonly List<PassCorrelationSample> _passCorrelationSamples = new();
    private readonly Dictionary<string, int> _winsByFinalDayEvent = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _winsByStartingDayEvent = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _cardPlaysByEffect = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _dayEventOccurrences = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, int> _turnDistribution = new();
    private int _minTurns = int.MaxValue;
    private int _maxTurns;
    private readonly Dictionary<string, RoleWinTracker> _roleWinTrackers = new(StringComparer.OrdinalIgnoreCase);

    public void Add(SingleMatchOutcome outcome)
    {
        lock (_lock)
        {
            if (outcome.IsStalemate)
                _stalemates++;
            else if (outcome.Winner == WinTeam.Humans)
                _humanWins++;
            else if (outcome.Winner == WinTeam.Zombies)
                _zombieWins++;

            _totalTurns += outcome.Turns;
            _totalFriendlyFire += outcome.FriendlyFireCount;
            _totalVoteEliminations += outcome.VoteEliminations;
            _totalCardPlays += outcome.TotalCardPlays;

            _totalHumanPasses += outcome.PassCounts.Human;
            _totalZombiePasses += outcome.PassCounts.Zombie;
            _totalPowerZombiePasses += outcome.PassCounts.PowerZombie;
            _totalHumanCardPlays += outcome.CardPlayCounts.Human;
            _totalZombieCardPlays += outcome.CardPlayCounts.Zombie;
            _totalPowerZombieRoleCardPlays += outcome.CardPlayCounts.PowerZombie;
            _totalHumanPlayersAtStart += outcome.HumanPlayersAtStart;
            _totalZombiePlayersAtStart += outcome.ZombiePlayersAtStart;
            _totalPowerZombiePlayersAtStart += outcome.PowerZombiePlayersAtStart;

            _totalSuccessfulZombieInfections += outcome.InfectionCounts.SuccessfulZombieInfections;
            _totalBlockedZombieInfections += outcome.InfectionCounts.BlockedZombieInfections;
            _totalSuccessfulPowerZombieInfections += outcome.InfectionCounts.SuccessfulPowerZombieInfections;
            _totalZombieCures += outcome.InfectionCounts.ZombieCures;
            _totalHumansConvertedToZombies += outcome.InfectionCounts.HumansConvertedToZombies;
            _totalZombiesConvertedToHumans += outcome.InfectionCounts.ZombiesConvertedToHumans;
            _totalHealCardPlays += outcome.InfectionCounts.HealCardPlays;
            _totalPowerZombieCardPlays += outcome.InfectionCounts.PowerZombieCardPlays;
            _totalBlockedPowerZombieInfections += outcome.InfectionCounts.BlockedPowerZombieInfections;
            _totalPowerZombieEliminations += outcome.InfectionCounts.PowerZombieEliminations;
            _totalInfectedWithRemainingShield += outcome.InfectionCounts.InfectedWithRemainingShield;
            _totalDisabledShotgunsAfterInfection += outcome.InfectionCounts.DisabledShotgunsAfterInfection;
            _totalDisabledHealsAfterInfection += outcome.InfectionCounts.DisabledHealsAfterInfection;

            _totalVotesCast += outcome.VotingCounts.TotalVotesCast;
            _totalVotesTargetingInfected += outcome.VotingCounts.VotesTargetingInfected;
            _totalZombieVoteEliminations += outcome.VotingCounts.ZombieVoteEliminations;
            _totalPowerZombieVoteEliminations += outcome.VotingCounts.PowerZombieVoteEliminations;
            _totalStartingZombies += outcome.VotingCounts.StartingZombies;
            _totalStartingPowerZombies += outcome.VotingCounts.StartingPowerZombies;
            _sumSuspicionTowardHumans += outcome.VotingCounts.SumSuspicionTowardHumans;
            _sumSuspicionTowardZombies += outcome.VotingCounts.SumSuspicionTowardZombies;
            _sumSuspicionTowardPowerZombies += outcome.VotingCounts.SumSuspicionTowardPowerZombies;
            _suspicionSamplesTowardHumans += outcome.VotingCounts.SuspicionSamplesTowardHumans;
            _suspicionSamplesTowardZombies += outcome.VotingCounts.SuspicionSamplesTowardZombies;
            _suspicionSamplesTowardPowerZombies += outcome.VotingCounts.SuspicionSamplesTowardPowerZombies;

            if (!outcome.IsStalemate)
            {
                _passCorrelationSamples.Add(new PassCorrelationSample
                {
                    HumanPasses = outcome.PassCounts.Human,
                    ZombiePasses = outcome.PassCounts.Zombie,
                    PowerZombiePasses = outcome.PassCounts.PowerZombie,
                    HumanWin = outcome.HumanWon ? 1.0 : 0.0
                });
            }

            Increment(_turnDistribution, outcome.Turns);
            if (outcome.Turns < _minTurns)
                _minTurns = outcome.Turns;
            if (outcome.Turns > _maxTurns)
                _maxTurns = outcome.Turns;

            foreach (var (dayEvent, count) in outcome.DayEventCounts)
                _dayEventOccurrences[dayEvent] = _dayEventOccurrences.GetValueOrDefault(dayEvent) + count;

            if (!outcome.IsStalemate)
            {
                Increment(_winsByFinalDayEvent, outcome.FinalDayEvent.ToString());
                Increment(_winsByStartingDayEvent, outcome.StartingDayEvent.ToString());
            }

            foreach (var (effect, count) in outcome.CardPlaysByEffect)
                _cardPlaysByEffect[effect] = _cardPlaysByEffect.GetValueOrDefault(effect) + count;

            foreach (var role in outcome.StartingRolesPresent.Keys)
            {
                var key = role.ToString();
                if (!_roleWinTrackers.TryGetValue(key, out var tracker))
                {
                    tracker = new RoleWinTracker();
                    _roleWinTrackers[key] = tracker;
                }

                tracker.MatchesWithRole++;
                if (outcome.HumanWon)
                    tracker.HumanWinsWhenPresent++;
            }
        }
    }

    public void RecordDayEvent(DayEventType dayEvent)
    {
        lock (_lock)
        {
            Increment(_dayEventOccurrences, dayEvent.ToString());
        }
    }

    public BalanceStatistics Build(int totalMatches)
    {
        lock (_lock)
        {
            var completed = _humanWins + _zombieWins;
            return new BalanceStatistics
            {
                TotalMatches = totalMatches,
                HumanWins = _humanWins,
                ZombieWins = _zombieWins,
                Stalemates = _stalemates,
                HumanWinRate = completed > 0 ? Math.Round(_humanWins * 100.0 / completed, 2) : 0,
                ZombieWinRate = completed > 0 ? Math.Round(_zombieWins * 100.0 / completed, 2) : 0,
                StalemateRate = totalMatches > 0 ? Math.Round(_stalemates * 100.0 / totalMatches, 2) : 0,
                AverageTurns = totalMatches > 0 ? Math.Round(_totalTurns / (double)totalMatches, 2) : 0,
                AverageFriendlyFirePerMatch = totalMatches > 0 ? Math.Round(_totalFriendlyFire / (double)totalMatches, 2) : 0,
                AverageEliminationsByVote = totalMatches > 0 ? Math.Round(_totalVoteEliminations / (double)totalMatches, 2) : 0,
                AverageCardPlaysPerMatch = totalMatches > 0 ? Math.Round(_totalCardPlays / (double)totalMatches, 2) : 0,
                WinsByFinalDayEvent = new Dictionary<string, int>(_winsByFinalDayEvent),
                WinsByStartingDayEvent = new Dictionary<string, int>(_winsByStartingDayEvent),
                CardPlaysByEffect = new Dictionary<string, int>(_cardPlaysByEffect),
                DayEventOccurrences = new Dictionary<string, int>(_dayEventOccurrences),
                TurnDistribution = new Dictionary<int, int>(_turnDistribution),
                MinTurns = _minTurns == int.MaxValue ? 0 : _minTurns,
                MaxTurns = _maxTurns,
                WinRateWhenRolePresentAtStart = _roleWinTrackers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.MatchesWithRole > 0
                        ? Math.Round(kvp.Value.HumanWinsWhenPresent * 100.0 / kvp.Value.MatchesWithRole, 2)
                        : 0)
            };
        }
    }

    public PassUsageTelemetry BuildPassTelemetry(int totalMatches)
    {
        lock (_lock)
        {
            var totalDays = _totalTurns;
            return new PassUsageTelemetry
            {
                TotalDays = totalDays,
                Humans = BuildRoleStats(_totalHumanPasses, totalMatches, totalDays, _totalHumanPlayersAtStart),
                Zombies = BuildRoleStats(_totalZombiePasses, totalMatches, totalDays, _totalZombiePlayersAtStart),
                PowerZombies = BuildRoleStats(_totalPowerZombiePasses, totalMatches, totalDays, _totalPowerZombiePlayersAtStart),
                HumanActions = BuildActionStats(_totalHumanPasses, _totalHumanCardPlays, totalMatches),
                ZombieActions = BuildActionStats(_totalZombiePasses, _totalZombieCardPlays, totalMatches),
                PowerZombieActions = BuildActionStats(_totalPowerZombiePasses, _totalPowerZombieRoleCardPlays, totalMatches),
                Correlation = BuildPassCorrelation(_passCorrelationSamples)
            };
        }
    }

    public VotingTelemetry BuildVotingTelemetry(int totalMatches)
    {
        lock (_lock)
        {
            return new VotingTelemetry
            {
                TotalVotesCast = _totalVotesCast,
                TotalVoteEliminations = _totalZombieVoteEliminations + _totalPowerZombieVoteEliminations,
                AverageSuspicionTowardHumans = Average(_sumSuspicionTowardHumans, _suspicionSamplesTowardHumans),
                AverageSuspicionTowardZombies = Average(_sumSuspicionTowardZombies, _suspicionSamplesTowardZombies),
                AverageSuspicionTowardPowerZombies = Average(_sumSuspicionTowardPowerZombies, _suspicionSamplesTowardPowerZombies),
                VoteAccuracy = Percent(_totalVotesTargetingInfected, _totalVotesCast),
                ZombieEliminationRate = Percent(_totalZombieVoteEliminations, _totalStartingZombies),
                PowerZombieEliminationRate = Percent(_totalPowerZombieVoteEliminations, _totalStartingPowerZombies)
            };
        }
    }

    private static double Average(double sum, long count) =>
        count > 0 ? Math.Round(sum / count, 4) : 0;

    public InfectionTelemetry BuildInfectionTelemetry(int totalMatches)
    {
        lock (_lock)
        {
            var zombieAttempts = _totalSuccessfulZombieInfections + _totalBlockedZombieInfections;
            return new InfectionTelemetry
            {
                TotalSuccessfulZombieInfections = _totalSuccessfulZombieInfections,
                TotalBlockedZombieInfections = _totalBlockedZombieInfections,
                TotalSuccessfulPowerZombieInfections = _totalSuccessfulPowerZombieInfections,
                TotalZombieCures = _totalZombieCures,
                TotalHumansConvertedToZombies = _totalHumansConvertedToZombies,
                TotalZombiesConvertedToHumans = _totalZombiesConvertedToHumans,
                TotalHealCardPlays = _totalHealCardPlays,
                TotalPowerZombieCardPlays = _totalPowerZombieCardPlays,
                AverageSuccessfulInfectionsPerMatch = Rate(_totalSuccessfulZombieInfections, totalMatches),
                AverageFailedInfectionsPerMatch = Rate(_totalBlockedZombieInfections, totalMatches),
                AverageHealsPerMatch = Rate(_totalZombieCures, totalMatches),
                AverageHumanToZombieConversionsPerMatch = Rate(_totalHumansConvertedToZombies, totalMatches),
                AverageZombieToHumanConversionsPerMatch = Rate(_totalZombiesConvertedToHumans, totalMatches),
                InfectionSuccessRate = Percent(_totalSuccessfulZombieInfections, zombieAttempts),
                PowerZombieInfectionSuccessRate = Percent(_totalSuccessfulPowerZombieInfections, _totalPowerZombieCardPlays),
                AverageSuccessfulPowerZombieInfectionsPerMatch = Rate(_totalSuccessfulPowerZombieInfections, totalMatches),
                AverageBlockedPowerZombieInfectionsPerMatch = Rate(_totalBlockedPowerZombieInfections, totalMatches),
                PowerZombieKillRate = Percent(_totalPowerZombieEliminations, _totalStartingPowerZombies),
                HealEffectivenessRate = Percent(_totalZombieCures, _totalHealCardPlays),
                TotalInfectedWithRemainingShield = _totalInfectedWithRemainingShield,
                TotalDisabledShotgunsAfterInfection = _totalDisabledShotgunsAfterInfection,
                TotalDisabledHealsAfterInfection = _totalDisabledHealsAfterInfection,
                AverageInfectedWithRemainingShieldPerMatch = Rate(_totalInfectedWithRemainingShield, totalMatches),
                AverageDisabledShotgunsAfterInfectionPerMatch = Rate(_totalDisabledShotgunsAfterInfection, totalMatches),
                AverageDisabledHealsAfterInfectionPerMatch = Rate(_totalDisabledHealsAfterInfection, totalMatches)
            };
        }
    }

    private static double Rate(long total, int matches) =>
        matches > 0 ? Math.Round(total / (double)matches, 4) : 0;

    private static double Percent(long numerator, long denominator) =>
        denominator > 0 ? Math.Round(numerator * 100.0 / denominator, 2) : 0;

    private static RolePassUsageStats BuildRoleStats(long totalPasses, int totalMatches, long totalDays, long totalPlayersAtStart)
    {
        return new RolePassUsageStats
        {
            TotalPasses = totalPasses,
            AveragePassesPerMatch = totalMatches > 0 ? Math.Round(totalPasses / (double)totalMatches, 4) : 0,
            AveragePassesPerDay = totalDays > 0 ? Math.Round(totalPasses / (double)totalDays, 4) : 0,
            AveragePassesPerPlayer = totalPlayersAtStart > 0 ? Math.Round(totalPasses / (double)totalPlayersAtStart, 4) : 0
        };
    }

    private static RoleActionUsageStats BuildActionStats(long totalPasses, long totalCardPlays, int totalMatches)
    {
        var totalActions = totalPasses + totalCardPlays;
        return new RoleActionUsageStats
        {
            TotalPasses = totalPasses,
            TotalCardPlays = totalCardPlays,
            AveragePassesPerMatch = totalMatches > 0 ? Math.Round(totalPasses / (double)totalMatches, 4) : 0,
            AverageCardPlaysPerMatch = totalMatches > 0 ? Math.Round(totalCardPlays / (double)totalMatches, 4) : 0,
            PassRatePercent = totalActions > 0 ? Math.Round(totalPasses * 100.0 / totalActions, 2) : 0
        };
    }

    private static PassWinRateCorrelation BuildPassCorrelation(IReadOnlyList<PassCorrelationSample> samples)
    {
        return new PassWinRateCorrelation
        {
            HumanPassCountVsHumanWin = PearsonCorrelation(
                samples.Select(s => (double)s.HumanPasses),
                samples.Select(s => s.HumanWin)),
            ZombiePassCountVsHumanWin = PearsonCorrelation(
                samples.Select(s => (double)s.ZombiePasses),
                samples.Select(s => s.HumanWin)),
            PowerZombiePassCountVsHumanWin = PearsonCorrelation(
                samples.Select(s => (double)s.PowerZombiePasses),
                samples.Select(s => s.HumanWin)),
            TotalPassCountVsHumanWin = PearsonCorrelation(
                samples.Select(s => (double)(s.HumanPasses + s.ZombiePasses + s.PowerZombiePasses)),
                samples.Select(s => s.HumanWin))
        };
    }

    public static double PearsonCorrelation(IEnumerable<double> xs, IEnumerable<double> ys)
    {
        var xList = xs.ToList();
        var yList = ys.ToList();
        if (xList.Count != yList.Count || xList.Count < 2)
            return 0;

        var n = xList.Count;
        var sumX = xList.Sum();
        var sumY = yList.Sum();
        var sumXY = xList.Zip(yList, (x, y) => x * y).Sum();
        var sumX2 = xList.Sum(x => x * x);
        var sumY2 = yList.Sum(y => y * y);

        var numerator = n * sumXY - sumX * sumY;
        var denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
        if (denominator == 0)
            return 0;

        return Math.Round(numerator / denominator, 4);
    }

    private static void Increment(Dictionary<string, int> dict, string key) =>
        dict[key] = dict.GetValueOrDefault(key) + 1;

    private static void Increment(Dictionary<int, int> dict, int key) =>
        dict[key] = dict.GetValueOrDefault(key) + 1;

    private sealed class RoleWinTracker
    {
        public int MatchesWithRole { get; set; }
        public int HumanWinsWhenPresent { get; set; }
    }

    private sealed class PassCorrelationSample
    {
        public int HumanPasses { get; init; }
        public int ZombiePasses { get; init; }
        public int PowerZombiePasses { get; init; }
        public double HumanWin { get; init; }
    }
}
