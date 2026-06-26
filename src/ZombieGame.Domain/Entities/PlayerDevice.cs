namespace ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

public class PlayerDevice
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public User Player { get; set; } = null!;
    public ICollection<PlayerSession> Sessions { get; set; } = new List<PlayerSession>();
}
