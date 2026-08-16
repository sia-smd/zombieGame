namespace ZombieGame.Application.Bots.Cognition;

using ZombieGame.Domain.Models;

public static class BotMemoryService
{
    public const double PersonalMemoryBonus = 1.5;
    public const double DecayFactor = 0.9;
    public const double MinWeightToKeep = 0.4;

    public static void EnsureCognition(GameSessionState state, Guid botUserId)
    {
        if (!state.BotCognition.ContainsKey(botUserId))
            state.BotCognition[botUserId] = new BotCognitionState();
    }

    public static void AddMemory(
        GameSessionState state,
        Guid observerBotId,
        Guid subjectUserId,
        BotMemoryKind kind,
        double baseWeight,
        bool personal = false)
    {
        EnsureCognition(state, observerBotId);
        var cognition = state.BotCognition[observerBotId];
        var weight = baseWeight * (personal ? PersonalMemoryBonus : 1.0);

        var existing = cognition.Memories.FirstOrDefault(m =>
            m.SubjectUserId == subjectUserId && m.Kind == kind);

        if (existing is not null)
            existing.Weight += weight;
        else
            cognition.Memories.Add(new BotMemoryEntry
            {
                SubjectUserId = subjectUserId,
                Kind = kind,
                Weight = weight,
                DayRecorded = state.TurnNumber,
                IsPersonal = personal
            });

        BotBeliefService.ApplyMemoryBeliefDelta(state, observerBotId, subjectUserId, kind);
    }

    public static void DecayAllMemories(GameSessionState state)
    {
        foreach (var cognition in state.BotCognition.Values)
        {
            for (var i = cognition.Memories.Count - 1; i >= 0; i--)
            {
                cognition.Memories[i].Weight *= DecayFactor;
                if (cognition.Memories[i].Weight < MinWeightToKeep)
                    cognition.Memories.RemoveAt(i);
            }
        }
    }

    public static double GetMemorySuspicion(GameSessionState state, Guid observerId, Guid targetId)
    {
        if (!state.BotCognition.TryGetValue(observerId, out var cognition))
            return 0;

        return cognition.Memories
            .Where(m => m.SubjectUserId == targetId)
            .Sum(m => MemoryKindToSuspicion(m.Kind) * m.Weight);
    }

    private static double MemoryKindToSuspicion(BotMemoryKind kind) => kind switch
    {
        BotMemoryKind.RepeatedPass => 0.8,
        BotMemoryKind.PublicAction => 0.2,
        BotMemoryKind.FriendlyFireWitnessed => 2.5,
        BotMemoryKind.ZombieRevealWitnessed => 3.0,
        BotMemoryKind.ShieldBlockWitnessed => -0.5,
        BotMemoryKind.HealWitnessed => -1.0,
        BotMemoryKind.ZombieKillWitnessed => -1.5,
        BotMemoryKind.PlayerEliminated => 0.5,
        BotMemoryKind.VotedAgainstMe => 1.2,
        BotMemoryKind.DefendedMe => -1.0,
        BotMemoryKind.SurvivedSuspiciously => 1.0,
        _ => 0
    };
}
