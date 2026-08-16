namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.GameRules;

public enum BalanceScenarioType
{
    Standard,
    StrongHumans,
    StrongZombies,
    NoPowerZombie
}

public static class BalanceScenarioProfiles
{
    public static RoleComposition GetComposition(BalanceScenarioType scenario, int playerCount)
    {
        _ = playerCount;
        return scenario switch
        {
            BalanceScenarioType.StrongZombies => new RoleComposition { ZombieCount = 3, PowerZombieCount = 1 },
            BalanceScenarioType.NoPowerZombie => new RoleComposition { ZombieCount = 3, PowerZombieCount = 0 },
            _ => new RoleComposition { ZombieCount = 1, PowerZombieCount = 1 }
        };
    }

    public static string GetDisplayName(BalanceScenarioType scenario) => scenario switch
    {
        BalanceScenarioType.StrongHumans => "Strong Humans",
        BalanceScenarioType.StrongZombies => "Strong Zombies",
        BalanceScenarioType.NoPowerZombie => "No PowerZombie",
        _ => "Standard (10 Human / 1 Zombie / 1 PowerZombie)"
    };

    public static string GetDisplayNameFa(BalanceScenarioType scenario) => scenario switch
    {
        BalanceScenarioType.StrongHumans => "انسان‌های قوی",
        BalanceScenarioType.StrongZombies => "زامبی‌های قوی",
        BalanceScenarioType.NoPowerZombie => "بدون PowerZombie",
        _ => "استاندارد (۱۰ انسان / ۱ زامبی / ۱ PowerZombie)"
    };
}
