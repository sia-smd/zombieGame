namespace ZombieGame.Application.Options;

public class RedisSettings
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "localhost:6379";
    public int SessionTtlHours { get; set; } = 48;
    public string KeyPrefix { get; set; } = "zg";
}
