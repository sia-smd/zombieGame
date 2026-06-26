namespace ZombieGame.Domain.Entities;

public class GameActionLog
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string? StateSnapshotJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Match Match { get; set; } = null!;
    public User User { get; set; } = null!;
}
