namespace ZombieGame.Application.Options;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ZombieGame";
    public string Audience { get; set; } = "ZombieGameClient";
    public int ExpirationMinutes { get; set; } = 1440;
    public int AccessTokenExpirationDays { get; set; } = 30;
    public int RefreshTokenExpirationDays { get; set; } = 180;
}
