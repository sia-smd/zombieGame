namespace ZombieGame.Infrastructure.Logging;

public sealed class DailyExceptionLogOptions
{
    public const string SectionName = "DailyExceptionLog";

    /// <summary>Folder under the app content root. Default: logs</summary>
    public string Directory { get; set; } = "logs";

    /// <summary>File name pattern. {0} = yyyy-MM-dd</summary>
    public string FileNameFormat { get; set; } = "exceptions-{0:yyyy-MM-dd}.txt";
}
