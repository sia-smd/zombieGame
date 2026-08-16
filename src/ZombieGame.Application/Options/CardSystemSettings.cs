namespace ZombieGame.Application.Options;

public sealed class CardDistributionSettings
{
    public int Shotgun { get; set; } = 50;
    public int Heal { get; set; } = 30;
    public int Shield { get; set; } = 20;
}

public sealed class InventorySettings
{
    public int MaxSlots { get; set; } = 4;
    public int MaxShield { get; set; } = 1;
    public int MaxHeal { get; set; } = 1;
}
