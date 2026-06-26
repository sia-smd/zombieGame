namespace ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? MatchId { get; set; }
    public TransactionType Type { get; set; }
    public int Amount { get; set; }
    public int BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
