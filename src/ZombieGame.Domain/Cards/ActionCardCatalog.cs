namespace ZombieGame.Domain.Cards;

using ZombieGame.Domain.Enums;

/// <summary>
/// Permanent inventory action cards (never consumed), distinct from role-identity images.
/// Humans always hold Visitor; infected roles always hold Poison; everyone holds Pass.
/// </summary>
public static class ActionCardCatalog
{
    public static readonly Guid Visitor = Guid.Parse("11111111-1111-1111-1111-111111111109");
    public static readonly Guid ZombiePoison = Guid.Parse("11111111-1111-1111-1111-111111111110");
    public static readonly Guid Pass = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Legacy alias — same id as <see cref="Visitor"/>.</summary>
    public static readonly Guid HumanAction = Visitor;

    public static Guid? GetRoleActionFor(PlayerRole role) => role switch
    {
        PlayerRole.Human => Visitor,
        PlayerRole.Zombie or PlayerRole.PowerZombie => ZombiePoison,
        _ => null
    };

    public static bool IsPermanentInventoryCard(Guid cardId) =>
        cardId == Visitor || cardId == ZombiePoison || cardId == Pass;

    public static bool IsActionCard(Guid cardId) => IsPermanentInventoryCard(cardId);
}
