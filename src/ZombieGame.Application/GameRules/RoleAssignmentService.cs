namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public interface IRoleAssignmentService
{
    void AssignRoles(GameSessionState state, int playerCount);
}

public sealed class RoleAssignmentService : IRoleAssignmentService
{
    public void AssignRoles(GameSessionState state, int playerCount)
    {
        var alive = state.Players.Where(p => p.IsAlive).ToList();
        if (alive.Count == 0) return;

        foreach (var player in alive)
        {
            player.Role = PlayerRole.Human;
            player.RemainingHealth = 1;
            player.HasShield = false;
            player.ShotgunHitCount = 0;
        }

        var shuffled = alive.OrderBy(_ => Random.Shared.Next()).ToList();

        var (powerZombieCount, zombieCount) = playerCount switch
        {
            >= 16 => (1, 3),
            _ => (1, 1)
        };

        var index = 0;
        for (var i = 0; i < powerZombieCount && index < shuffled.Count; i++, index++)
        {
            shuffled[index].Role = PlayerRole.PowerZombie;
            shuffled[index].RemainingHealth = 2;
        }

        for (var i = 0; i < zombieCount && index < shuffled.Count; i++, index++)
        {
            shuffled[index].Role = PlayerRole.Zombie;
            shuffled[index].RemainingHealth = 1;
        }
    }
}
