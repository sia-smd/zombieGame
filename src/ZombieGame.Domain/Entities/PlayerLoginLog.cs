namespace ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

public class PlayerLoginLog
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
    public LoginResult Result { get; set; }
    public string Action { get; set; } = string.Empty;

    public User Player { get; set; } = null!;
}
