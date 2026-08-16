namespace ZombieGame.Application.Options;

public class ClientSettings
{
    public const string SectionName = "Client";

    /// <summary>Current engine/API version. Clients compare this for update prompts.</summary>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>Clients below this version must update before playing.</summary>
    public string MinClientVersion { get; set; } = "1.0.0";
}
