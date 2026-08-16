namespace ZombieGame.Application.Bots.Cognition;

using ZombieGame.Application.Bots.Discussion;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Simulation;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

/// <summary>
/// Records publicly observable events into suspicion + per-bot memory.
/// Never uses hidden roles — only what witnesses could see.
/// </summary>
public static class BotObservationRecorder
{
    public static void InitializeBots(
        GameSessionState state,
        Random random,
        BalanceScenarioType scenario = BalanceScenarioType.Standard)
    {
        foreach (var player in state.Players.Where(p => p.IsBot))
        {
            BotMemoryService.EnsureCognition(state, player.UserId);
            var personality = BotPersonalityFactory.Create(random);
            BotPersonalityFactory.ApplyRoleAndScenarioModifiers(personality, player.Role, scenario);
            state.BotCognition[player.UserId].Personality = personality;
        }

        BotBeliefService.InitializeAllBeliefs(state);
        BotReputationService.InitializeAll(state);
        SuspicionScoring.EnsureInitialized(state);
    }

    public static void OnDayStart(GameSessionState state)
    {
        state.DaysSinceLastElimination++;
        state.DiscussionMessagesThisDay.Clear();
        state.DiscussionAnnouncementsThisDay.Clear();
        SuspicionScoring.ResetDayActivities(state);
        BotMemoryService.DecayAllMemories(state);
    }

    public static void OnDayEnd(GameSessionState state) =>
        SuspicionScoring.FinalizeDay(state);

    public static void OnPass(GameSessionState state, Guid actorId, IEnumerable<Guid>? witnesses = null)
    {
        SuspicionScoring.RecordPass(state, actorId);
        RecordForWitnesses(state, witnesses, actorId, BotMemoryKind.RepeatedPass, 1, personal: false);
    }

    public static void OnPublicAction(
        GameSessionState state,
        Guid actorId,
        IEnumerable<Guid>? witnesses = null)
    {
        SuspicionScoring.RecordUsefulAction(state, actorId);
        RecordForWitnesses(state, witnesses, actorId, BotMemoryKind.PublicAction, 1, personal: false);
    }

    public static void OnCardOutcome(
        GameSessionState state,
        Guid actorId,
        Guid targetId,
        CardEffectResult result,
        string effectKey,
        IEnumerable<Guid>? witnesses = null)
    {
        SuspicionScoring.ApplyCardOutcome(state, actorId, targetId, result, effectKey);

        var witnessList = ResolveWitnesses(state, witnesses).ToList();
        foreach (var witnessId in witnessList)
        {
            if (!IsBot(state, witnessId))
                continue;

            BotMemoryService.EnsureCognition(state, witnessId);
            var cognition = state.BotCognition[witnessId];

            if (result.FriendlyFire)
                BotMemoryService.AddMemory(state, witnessId, actorId, BotMemoryKind.FriendlyFireWitnessed, 3);

            switch (result.TelemetryKind)
            {
                case CardEffectTelemetryKind.ZombieInfectionSucceeded:
                case CardEffectTelemetryKind.PowerZombieInfectionSucceeded:
                    cognition.ObservedInfectors.Add(actorId);
                    cognition.PubliclyKnownInfected.Add(targetId);
                    BotMemoryService.AddMemory(state, witnessId, actorId, BotMemoryKind.ZombieRevealWitnessed, 3);
                    BotMemoryService.AddMemory(state, witnessId, targetId, BotMemoryKind.SurvivedSuspiciously, 1);
                    BotBeliefService.SetBelief(state, witnessId, actorId, Math.Max(BotBeliefService.GetBelief(state, witnessId, actorId), 85));
                    BotBeliefService.SetBelief(state, witnessId, targetId, Math.Max(BotBeliefService.GetBelief(state, witnessId, targetId), 90));
                    break;
                case CardEffectTelemetryKind.ZombieInfectionBlockedByShield:
                case CardEffectTelemetryKind.PowerZombieInfectionBlockedByShield:
                    BotMemoryService.AddMemory(state, witnessId, targetId, BotMemoryKind.ShieldBlockWitnessed, 1);
                    break;
                case CardEffectTelemetryKind.ZombieCured:
                    BotMemoryService.AddMemory(state, witnessId, actorId, BotMemoryKind.HealWitnessed, 2);
                    cognition.PubliclyKnownInfected.Remove(targetId);
                    BotBeliefService.SetBelief(state, witnessId, targetId, 15);
                    break;
                case CardEffectTelemetryKind.PowerZombieDemoted:
                    BotMemoryService.AddMemory(state, witnessId, actorId, BotMemoryKind.HealWitnessed, 2);
                    cognition.PubliclyKnownInfected.Add(targetId);
                    BotBeliefService.SetBelief(state, witnessId, targetId, Math.Max(BotBeliefService.GetBelief(state, witnessId, targetId), 70));
                    break;
            }

            if (result.TargetKilled && witnessList.Contains(witnessId))
            {
                var isPersonal = witnessId == targetId || witnessId == actorId;
                BotMemoryService.AddMemory(state, witnessId, actorId, BotMemoryKind.ZombieKillWitnessed, 2, isPersonal);
            }
        }

        if (result.FriendlyFire)
            BotReputationService.OnFriendlyFireWitnessed(state, actorId);

        if (result.TelemetryKind is CardEffectTelemetryKind.ZombieInfectionSucceeded
            or CardEffectTelemetryKind.PowerZombieInfectionSucceeded)
            state.PublicInfectionEventCount++;

        if (result.TelemetryKind is CardEffectTelemetryKind.ZombieCured
            or CardEffectTelemetryKind.PowerZombieDemoted)
            BotReputationService.OnHelpfulActionWitnessed(state, actorId);

        if (witnessList.Contains(actorId))
            BotMemoryService.AddMemory(state, actorId, targetId, BotMemoryKind.PublicAction, 1, personal: true);
    }

    public static void OnElimination(GameSessionState state, Guid eliminatedId, PlayerRole? revealedRole)
    {
        state.DaysSinceLastElimination = 0;

        if (revealedRole is PlayerRole.Zombie or PlayerRole.PowerZombie)
        {
            foreach (var bot in state.Players.Where(p => p.IsBot))
            {
                BotMemoryService.EnsureCognition(state, bot.UserId);
                state.BotCognition[bot.UserId].PubliclyKnownInfected.Add(eliminatedId);
                BotBeliefService.SetBelief(state, bot.UserId, eliminatedId, 100);
            }
        }

        foreach (var bot in state.Players.Where(p => p.IsBot && p.IsAlive))
            BotMemoryService.AddMemory(state, bot.UserId, eliminatedId, BotMemoryKind.PlayerEliminated, 1);

        var wasZombie = revealedRole is PlayerRole.Zombie or PlayerRole.PowerZombie;
        foreach (var msg in state.DiscussionHistory.Where(m =>
                     m.MessageType == DiscussionMessageType.SuspectPlayer &&
                     m.TargetUserId == eliminatedId))
        {
            BotReputationService.OnAccusationValidated(state, msg.SpeakerUserId, eliminatedId, wasZombie);
        }
    }

    public static void OnVote(GameSessionState state, Guid voterId, Guid targetId)
    {
        if (!IsBot(state, targetId) || voterId == targetId)
            return;

        BotMemoryService.AddMemory(state, targetId, voterId, BotMemoryKind.VotedAgainstMe, 2, personal: true);
    }

    private static void RecordForWitnesses(
        GameSessionState state,
        IEnumerable<Guid>? witnesses,
        Guid subjectId,
        BotMemoryKind kind,
        double weight,
        bool personal)
    {
        foreach (var witnessId in ResolveWitnesses(state, witnesses))
        {
            if (!IsBot(state, witnessId))
                continue;

            var isPersonal = personal || witnessId == subjectId;
            BotMemoryService.AddMemory(state, witnessId, subjectId, kind, weight, isPersonal);
        }
    }

    private static IEnumerable<Guid> ResolveWitnesses(GameSessionState state, IEnumerable<Guid>? witnesses)
    {
        if (witnesses is not null)
            return witnesses.Where(id => state.GetPlayer(id)?.IsAlive == true);

        return state.AlivePlayers.Select(p => p.UserId);
    }

    private static bool IsBot(GameSessionState state, Guid userId) =>
        state.GetPlayer(userId)?.IsBot == true;
}
