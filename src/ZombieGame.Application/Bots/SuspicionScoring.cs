namespace ZombieGame.Application.Bots;

using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public static class SuspicionScoring
{
    public const double PassWeight = 0.75;
    public const double RepeatedPassWeight = 1.25;
    public const double NoUsefulActionWeight = 1.5;
    public const double FriendlyFireWeight = 3.0;
    public const double InfectedAssociationWeight = 0.6;
    public const double RevealedInfectionWeight = 2.5;
    public const double SuccessfulHealWeight = 2.0;
    public const double ZombieKillWeight = 2.5;
    public const double ShieldBlockWeight = 1.5;

    public static void EnsureInitialized(GameSessionState state)
    {
        foreach (var player in state.Players)
        {
            if (!state.SuspicionScores.ContainsKey(player.UserId))
                state.SuspicionScores[player.UserId] = new Dictionary<Guid, double>();

            if (!state.PlayerDayActivities.ContainsKey(player.UserId))
                state.PlayerDayActivities[player.UserId] = new PlayerDayActivity();
        }
    }

    public static void ResetDayActivities(GameSessionState state)
    {
        foreach (var player in state.AlivePlayers)
            state.PlayerDayActivities[player.UserId] = new PlayerDayActivity();
    }

    public static void RecordPass(GameSessionState state, Guid playerId)
    {
        EnsureInitialized(state);
        var activity = state.PlayerDayActivities[playerId];
        activity.PassCount++;

        AddPublicSuspicion(state, playerId, PassWeight);
        if (activity.PassCount >= 2)
            AddPublicSuspicion(state, playerId, RepeatedPassWeight);
    }

    public static void RecordUsefulAction(GameSessionState state, Guid playerId)
    {
        EnsureInitialized(state);
        state.PlayerDayActivities[playerId].UsefulActionCount++;
    }

    public static void FinalizeDay(GameSessionState state)
    {
        EnsureInitialized(state);
        foreach (var player in state.AlivePlayers)
        {
            var activity = state.PlayerDayActivities[player.UserId];
            if (activity.PassCount > 0 && activity.UsefulActionCount == 0)
                AddPublicSuspicion(state, player.UserId, NoUsefulActionWeight);
        }
    }

    public static void ApplyCardOutcome(
        GameSessionState state,
        Guid actorUserId,
        Guid targetUserId,
        CardEffectResult result,
        string effectKey)
    {
        EnsureInitialized(state);
        RecordUsefulAction(state, actorUserId);

        if (result.FriendlyFire)
            AddPublicSuspicion(state, actorUserId, FriendlyFireWeight);

        switch (result.TelemetryKind)
        {
            case CardEffectTelemetryKind.ZombieInfectionSucceeded:
                AddPublicSuspicion(state, actorUserId, RevealedInfectionWeight);
                ApplyInfectedAssociation(state, actorUserId, targetUserId);
                break;
            case CardEffectTelemetryKind.ZombieInfectionBlockedByShield:
                AddPublicSuspicion(state, targetUserId, -ShieldBlockWeight);
                break;
            case CardEffectTelemetryKind.PowerZombieInfectionSucceeded:
                AddPublicSuspicion(state, actorUserId, RevealedInfectionWeight);
                ApplyInfectedAssociation(state, actorUserId, targetUserId);
                break;
            case CardEffectTelemetryKind.PowerZombieInfectionBlockedByShield:
                AddPublicSuspicion(state, targetUserId, -ShieldBlockWeight);
                break;
            case CardEffectTelemetryKind.ZombieCured:
                AddPublicSuspicion(state, actorUserId, -SuccessfulHealWeight);
                break;
            case CardEffectTelemetryKind.PowerZombieDemoted:
                AddPublicSuspicion(state, actorUserId, -SuccessfulHealWeight / 2);
                break;
        }

        if (result.TargetKilled)
        {
            var target = state.GetPlayer(targetUserId);
            if (target?.Role is PlayerRole.Zombie or PlayerRole.PowerZombie)
                AddPublicSuspicion(state, actorUserId, -ZombieKillWeight);
        }
    }

    public static Guid PickHighestSuspicionTarget(GameSessionState state, Guid voterId)
    {
        EnsureInitialized(state);
        var candidates = state.AlivePlayers.Where(p => p.UserId != voterId).ToList();
        if (candidates.Count == 0)
            return voterId;

        return candidates
            .OrderByDescending(p => GetSuspicion(state, voterId, p.UserId))
            .ThenBy(p => p.SeatIndex)
            .First()
            .UserId;
    }

    public static double GetSuspicion(GameSessionState state, Guid observerId, Guid targetId)
    {
        if (!state.SuspicionScores.TryGetValue(observerId, out var targets))
            return 0;

        return targets.TryGetValue(targetId, out var score) ? score : 0;
    }

    public static void AddPublicSuspicion(GameSessionState state, Guid targetId, double delta)
    {
        EnsureInitialized(state);
        foreach (var observer in state.AlivePlayers)
        {
            if (observer.UserId == targetId)
                continue;

            var scores = state.SuspicionScores[observer.UserId];
            scores[targetId] = scores.GetValueOrDefault(targetId) + delta;
            if (scores[targetId] < 0)
                scores[targetId] = 0;
        }
    }

    private static void ApplyInfectedAssociation(GameSessionState state, Guid infectorId, Guid victimId)
    {
        foreach (var player in state.AlivePlayers)
        {
            if (player.UserId == infectorId || player.UserId == victimId)
                continue;

            var activity = state.PlayerDayActivities.GetValueOrDefault(player.UserId);
            if (activity is not null && activity.PassCount > 0)
                AddPublicSuspicion(state, player.UserId, InfectedAssociationWeight);
        }
    }
}
