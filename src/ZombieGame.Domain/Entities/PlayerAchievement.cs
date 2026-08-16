namespace ZombieGame.Domain.Entities;

public class PlayerAchievement
{
    public Guid PlayerId { get; set; }
    public Guid AchievementId { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    public int StatValueAtUnlock { get; set; }

    public User Player { get; set; } = null!;
    public Achievement Achievement { get; set; } = null!;
}
