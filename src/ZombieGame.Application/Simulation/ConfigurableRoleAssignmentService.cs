namespace ZombieGame.Application.Simulation;

using ZombieGame.Application.GameRules;
using ZombieGame.Domain.Models;

public sealed class ConfigurableRoleAssignmentService : IRoleAssignmentService
{
    private readonly RoleAssignmentService _inner = new();

    public RoleComposition? Composition { get; set; }

    public void AssignRoles(GameSessionState state, int playerCount, RoleComposition? composition = null) =>
        _inner.AssignRoles(state, playerCount, composition ?? Composition);
}
