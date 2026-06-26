namespace ZombieGame.Domain.Entities;

public class PlayerSession
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string AccessTokenJti { get; set; } = string.Empty;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpireDate { get; set; }
    public DateTime RefreshExpireDate { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? DeviceId { get; set; }

    public User Player { get; set; } = null!;
    public PlayerDevice? Device { get; set; }
}
