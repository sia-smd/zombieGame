namespace ZombieGame.Application.GameRules;

public sealed class RoleComposition
{
    public int ZombieCount { get; init; }
    public int PowerZombieCount { get; init; }

    public int HumanCount(int playerCount) =>
        playerCount - ZombieCount - PowerZombieCount;
}
