namespace ZombieGame.Application.Options;

public class AccountSettings
{
    public const string SectionName = "Account";

    public string DefaultAvatarId { get; set; } = "avatar_default_01";
    public string GuestNamePrefix { get; set; } = "Guest";
    public int MaxProfileNameLength { get; set; } = 20;
    public int AccessTokenExpirationDays { get; set; } = 30;
    public int RefreshTokenExpirationDays { get; set; } = 180;
    public int MobileVerificationCodeLength { get; set; } = 6;
    public int MobileVerificationExpirationMinutes { get; set; } = 10;
    public string[] AllowedAvatarIds { get; set; } =
    [
        "avatar_default_01",
        "avatar_zombie_01",
        "avatar_survivor_01",
        "avatar_medic_01",
        "avatar_hunter_01"
    ];
}
