namespace ZombieGame.Application.Bots.Cognition;

using ZombieGame.Domain.Models;

/// <summary>
/// Per-bot subjective belief about who might be a zombie. Never reads hidden roles.
/// </summary>
public static class BotBeliefService
{
    public const int DefaultPrior = 25;
    public const int MinBelief = 0;
    public const int MaxBelief = 100;

    public static void InitializeAllBeliefs(GameSessionState state)
    {
        foreach (var bot in state.Players.Where(p => p.IsBot))
            InitializeBeliefs(state, bot.UserId);
    }

    public static void InitializeBeliefs(GameSessionState state, Guid botUserId)
    {
        BotMemoryService.EnsureCognition(state, botUserId);
        var cognition = state.BotCognition[botUserId];
        cognition.ZombieBeliefs.Clear();

        foreach (var player in state.Players)
        {
            if (player.UserId == botUserId)
                cognition.ZombieBeliefs[player.UserId] = SelfBelief(player);
            else
                cognition.ZombieBeliefs[player.UserId] = DefaultPrior;
        }
    }

    public static int GetBelief(GameSessionState state, Guid observerId, Guid subjectId)
    {
        if (observerId == subjectId)
        {
            var self = state.GetPlayer(observerId);
            return self is null ? 0 : SelfBelief(self);
        }

        if (state.BotCognition.TryGetValue(observerId, out var cognition) &&
            cognition.ZombieBeliefs.TryGetValue(subjectId, out var belief))
            return belief;

        return DefaultPrior;
    }

    public static void AdjustBelief(GameSessionState state, Guid observerId, Guid subjectId, int delta)
    {
        if (observerId == subjectId || delta == 0)
            return;

        BotMemoryService.EnsureCognition(state, observerId);
        var cognition = state.BotCognition[observerId];
        var current = cognition.ZombieBeliefs.GetValueOrDefault(subjectId, DefaultPrior);
        cognition.ZombieBeliefs[subjectId] = Math.Clamp(current + delta, MinBelief, MaxBelief);
    }

    public static void SetBelief(GameSessionState state, Guid observerId, Guid subjectId, int value)
    {
        if (observerId == subjectId)
            return;

        BotMemoryService.EnsureCognition(state, observerId);
        state.BotCognition[observerId].ZombieBeliefs[subjectId] =
            Math.Clamp(value, MinBelief, MaxBelief);
    }

    public static void ApplyMemoryBeliefDelta(
        GameSessionState state,
        Guid observerId,
        Guid subjectId,
        BotMemoryKind kind)
    {
        var delta = kind switch
        {
            BotMemoryKind.RepeatedPass => 10,
            BotMemoryKind.FriendlyFireWitnessed => 15,
            BotMemoryKind.ZombieRevealWitnessed => 60,
            BotMemoryKind.ShieldBlockWitnessed => -20,
            BotMemoryKind.HealWitnessed => -30,
            BotMemoryKind.ZombieKillWitnessed => -40,
            BotMemoryKind.VotedAgainstMe => 12,
            BotMemoryKind.DefendedMe => -15,
            BotMemoryKind.SurvivedSuspiciously => 10,
            BotMemoryKind.PlayerEliminated => 5,
            _ => 0
        };

        if (delta != 0)
            AdjustBelief(state, observerId, subjectId, delta);
    }

    private static int SelfBelief(GamePlayerState player) =>
        player.IsInfectedTeam ? MaxBelief : MinBelief;
}
