namespace ZombieGame.Domain.Entities;

public class PlayerProfile
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageId { get; set; } = string.Empty;
    public int Level { get; set; } = 1;

    public User Player { get; set; } = null!;
}
