namespace ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

public class Match
{
    public Guid Id { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Waiting;
    public GamePhase CurrentPhase { get; set; } = GamePhase.Lobby;
    public string SessionToken { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int MaxPlayers { get; set; } = 8;
    public bool FillWithBots { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public Guid? WinnerUserId { get; set; }

    public ICollection<MatchPlayer> Players { get; set; } = new List<MatchPlayer>();
    public ICollection<GameActionLog> ActionLogs { get; set; } = new List<GameActionLog>();
}
