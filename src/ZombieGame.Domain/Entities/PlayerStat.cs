namespace ZombieGame.Domain.Entities;

public class PlayerStat
{
    public Guid PlayerId { get; set; }
    public string StatKey { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Player { get; set; } = null!;
}
