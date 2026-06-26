namespace ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

public class MatchPlayer
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public PlayerRole Role { get; set; } = PlayerRole.Unknown;
    public bool IsBot { get; set; }
    public bool IsAlive { get; set; } = true;
    public int SeatIndex { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Match Match { get; set; } = null!;
    public User User { get; set; } = null!;
}
