namespace ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

public class AccountRecoveryChallenge
{
    public Guid Id { get; set; }
    public Guid GuestPlayerId { get; set; }
    public Guid TargetPlayerId { get; set; }
    public AccountRecoveryPurpose Purpose { get; set; } = AccountRecoveryPurpose.RecoverAccount;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public User? Guest { get; set; }
    public User? Target { get; set; }
}
