namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public interface IWinConditionService
{
    WinTeam? Evaluate(GameSessionState state);
    bool IsHumanTeam(PlayerRole role) => role == PlayerRole.Human;
    bool IsZombieTeam(PlayerRole role) => role is PlayerRole.Zombie or PlayerRole.PowerZombie;
}

public sealed class WinConditionService : IWinConditionService
{
    public WinTeam? Evaluate(GameSessionState state)
    {
        var alive = state.AlivePlayers.ToList();
        if (alive.Count == 0) return WinTeam.Zombies;

        var aliveHumans = alive.Count(p => p.Role == PlayerRole.Human);
        var aliveZombies = alive.Count(p => p.Role == PlayerRole.Zombie);
        var alivePowerZombies = alive.Count(p => p.Role == PlayerRole.PowerZombie);

        if (aliveZombies == 0 && alivePowerZombies == 0)
            return WinTeam.Humans;

        if (aliveHumans == 0)
            return WinTeam.Zombies;

        return null;
    }
}
