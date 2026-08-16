namespace ZombieGame.Domain.Cards;

using ZombieGame.Domain.Enums;

public static class RoleCardCatalog
{
    public static readonly Guid Human = Guid.Parse("11111111-1111-1111-1111-111111111108");
    public static readonly Guid Infection = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid AlphaZombie = Guid.Parse("11111111-1111-1111-1111-111111111106");

    public static Guid GetRoleCardId(PlayerRole role) => role switch
    {
        PlayerRole.Human => Human,
        PlayerRole.Zombie => Infection,
        PlayerRole.PowerZombie => AlphaZombie,
        _ => Human
    };

    public static bool IsRoleCard(Guid cardId) =>
        cardId == Human || cardId == Infection || cardId == AlphaZombie;
}
