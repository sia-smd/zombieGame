namespace ZombieGame.Application.Bots.Discussion;

using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Domain.Models;

public static class BotReputationService
{
    public const int NeutralReputation = 50;
    public const int MinReputation = 0;
    public const int MaxReputation = 100;

    public static void Initialize(GameSessionState state, Guid botUserId)
    {
        BotMemoryService.EnsureCognition(state, botUserId);
        var cognition = state.BotCognition[botUserId];
        cognition.SpeakerReputation.Clear();

        foreach (var player in state.Players.Where(p => p.UserId != botUserId))
            cognition.SpeakerReputation[player.UserId] = NeutralReputation;
    }

    public static void InitializeAll(GameSessionState state)
    {
        foreach (var bot in state.Players.Where(p => p.IsBot))
            Initialize(state, bot.UserId);
    }

    public static int GetReputation(GameSessionState state, Guid observerId, Guid speakerId)
    {
        if (observerId == speakerId)
            return MaxReputation;

        if (state.BotCognition.TryGetValue(observerId, out var cognition) &&
            cognition.SpeakerReputation.TryGetValue(speakerId, out var reputation))
            return reputation;

        return NeutralReputation;
    }

    public static void AdjustReputation(GameSessionState state, Guid observerId, Guid speakerId, int delta)
    {
        if (observerId == speakerId || delta == 0)
            return;

        BotMemoryService.EnsureCognition(state, observerId);
        var cognition = state.BotCognition[observerId];
        var current = cognition.SpeakerReputation.GetValueOrDefault(speakerId, NeutralReputation);
        cognition.SpeakerReputation[speakerId] = Math.Clamp(current + delta, MinReputation, MaxReputation);
    }

    public static void SetReputation(GameSessionState state, Guid observerId, Guid speakerId, int value)
    {
        if (observerId == speakerId)
            return;

        BotMemoryService.EnsureCognition(state, observerId);
        state.BotCognition[observerId].SpeakerReputation[speakerId] =
            Math.Clamp(value, MinReputation, MaxReputation);
    }

    public static void OnAccusationValidated(GameSessionState state, Guid speakerId, Guid accusedId, bool wasZombie)
    {
        foreach (var bot in state.Players.Where(p => p.IsBot))
        {
            var delta = wasZombie ? 12 : -15;
            AdjustReputation(state, bot.UserId, speakerId, delta);
            if (!wasZombie)
                AdjustReputation(state, bot.UserId, accusedId, 5);
        }
    }

    public static void OnFriendlyFireWitnessed(GameSessionState state, Guid actorId)
    {
        foreach (var bot in state.Players.Where(p => p.IsBot))
            AdjustReputation(state, bot.UserId, actorId, -10);
    }

    public static void OnHelpfulActionWitnessed(GameSessionState state, Guid actorId)
    {
        foreach (var bot in state.Players.Where(p => p.IsBot))
            AdjustReputation(state, bot.UserId, actorId, 6);
    }
}
