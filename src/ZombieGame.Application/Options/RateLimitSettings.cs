namespace ZombieGame.Application.Options;

public class RateLimitSettings
{
    public const string SectionName = "RateLimits";

    public const string AuthPolicy = "account-auth";
    public const string SmsPolicy = "account-sms";

    public int AuthPermitLimit { get; set; } = 20;
    public int SmsPermitLimit { get; set; } = 5;
    public int WindowMinutes { get; set; } = 1;
}
