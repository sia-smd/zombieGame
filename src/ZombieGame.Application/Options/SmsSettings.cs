namespace ZombieGame.Application.Options;

public class SmsSettings
{
    public const string SectionName = "Sms";

    public const string MockProvider = "Mock";
    public const string KavenegarProvider = "Kavenegar";

    public string Provider { get; set; } = MockProvider;
    public KavenegarSmsSettings Kavenegar { get; set; } = new();

    public bool UsesKavenegar =>
        string.Equals(Provider, KavenegarProvider, StringComparison.OrdinalIgnoreCase);
}

public class KavenegarSmsSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Template { get; set; } = "verify";
    public string Sender { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.kavenegar.com/v1";
}
