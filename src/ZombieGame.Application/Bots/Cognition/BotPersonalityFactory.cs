namespace ZombieGame.Application.Bots.Cognition;

using ZombieGame.Application.Simulation;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public static class BotPersonalityFactory
{
    public static BotPersonality Create(Random random) => new()
    {
        Aggression = random.Next(0, 101),
        RiskTolerance = random.Next(0, 101),
        Trust = random.Next(0, 101),
        Patience = random.Next(0, 101),
        Confidence = random.Next(0, 101),
        TalkManipulation = random.Next(0, 101),
        CommunicationStyle = (CommunicationStyle)random.Next(0, 3)
    };

    public static void ApplyRoleAndScenarioModifiers(
        BotPersonality personality,
        PlayerRole role,
        BalanceScenarioType scenario)
    {
        if (role == PlayerRole.PowerZombie)
            ApplyPowerZombieModifiers(personality);

        switch (scenario)
        {
            case BalanceScenarioType.StrongHumans when role == PlayerRole.Human:
                personality.Confidence = Clamp(personality.Confidence + 15);
                personality.RiskTolerance = Clamp(personality.RiskTolerance + 12);
                personality.Trust = Clamp(personality.Trust + 10);
                break;
            case BalanceScenarioType.StrongZombies when role is PlayerRole.Zombie or PlayerRole.PowerZombie:
                personality.Aggression = Clamp(personality.Aggression + 18);
                personality.RiskTolerance = Clamp(personality.RiskTolerance + 12);
                personality.Patience = Clamp(personality.Patience - 15);
                break;
        }
    }

    public static void ApplyPowerZombieModifiers(BotPersonality personality)
    {
        personality.Aggression = Clamp(personality.Aggression + 20);
        personality.RiskTolerance = Clamp(personality.RiskTolerance + 20);
        personality.TalkManipulation = Clamp(personality.TalkManipulation + 10);

        if (personality.CommunicationStyle == CommunicationStyle.Quiet &&
            personality.TalkManipulation >= 30)
            personality.CommunicationStyle = CommunicationStyle.Balanced;
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
