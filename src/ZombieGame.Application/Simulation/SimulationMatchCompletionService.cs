namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.GameRules;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public sealed class SimulationMatchCompletionService : IMatchCompletionService
{
    public Task CompleteMatchAsync(Match match, GameSessionState state, WinTeam winningTeam, CancellationToken cancellationToken = default)
    {
        state.WinTeam = winningTeam;
        state.CurrentPhase = GamePhase.Resolution;
        match.Status = MatchStatus.Finished;
        match.FinishedAt = DateTime.UtcNow;
        match.CurrentPhase = GamePhase.Resolution;
        match.WinnerUserId = state.Players.FirstOrDefault(p => IsOnTeam(p.Role, winningTeam))?.UserId;
        return Task.CompletedTask;
    }

    private static bool IsOnTeam(PlayerRole role, WinTeam team) => team switch
    {
        WinTeam.Humans => role == PlayerRole.Human,
        WinTeam.Zombies => role is PlayerRole.Zombie or PlayerRole.PowerZombie,
        _ => false
    };
}
