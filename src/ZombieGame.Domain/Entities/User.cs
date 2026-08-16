namespace ZombieGame.Domain.Entities;

using ZombieGame.Domain.Enums;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PendingPhoneNumber { get; set; }
    public bool MobileVerified { get; set; }
    public string? MobileVerificationCodeHash { get; set; }
    public DateTime? MobileVerificationExpiresAt { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public AccountType AccountType { get; set; } = AccountType.Guest;
    public int Coins { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public PlayerProfile? Profile { get; set; }
    public ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<GameActionLog> GameActionLogs { get; set; } = new List<GameActionLog>();
    public ICollection<PlayerSession> Sessions { get; set; } = new List<PlayerSession>();
    public ICollection<PlayerDevice> Devices { get; set; } = new List<PlayerDevice>();
    public ICollection<PlayerLoginLog> LoginLogs { get; set; } = new List<PlayerLoginLog>();
    public ICollection<PlayerStat> Stats { get; set; } = new List<PlayerStat>();
    public ICollection<PlayerAchievement> Achievements { get; set; } = new List<PlayerAchievement>();
}
