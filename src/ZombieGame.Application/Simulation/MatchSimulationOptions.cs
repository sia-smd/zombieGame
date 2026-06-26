namespace ZombieGame.Application.Simulation;

public class MatchSimulationOptions
{
    public int MatchCount { get; set; } = 10_000;
    public int PlayerCount { get; set; } = 8;
    public int MaxTurnsPerMatch { get; set; } = 50;
    public int MaxCardPlaysPerBotPerDay { get; set; } = 2;
    public int? RandomSeed { get; set; }
    public int MaxParallelism { get; set; } = 0;
    public bool PassPenaltyMode { get; set; }
    /// <summary>0 = unlimited Pass actions per day.</summary>
    public int MaxPassActionsPerDay { get; set; }
    public bool UseSuspicionBasedVoting { get; set; } = true;
    public bool ShieldBlocksPowerZombieInfection { get; set; }
}
