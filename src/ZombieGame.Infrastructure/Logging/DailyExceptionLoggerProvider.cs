namespace ZombieGame.Infrastructure.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Writes exception / error entries to one UTF-8 text file per calendar day under logs/.
/// </summary>
public sealed class DailyExceptionLoggerProvider : ILoggerProvider
{
    private readonly DailyExceptionLogWriter _writer;
    private readonly string _categoryPrefix;

    public DailyExceptionLoggerProvider(IOptions<DailyExceptionLogOptions> options, string? contentRoot = null)
    {
        var opts = options.Value;
        var root = string.IsNullOrWhiteSpace(contentRoot)
            ? AppContext.BaseDirectory
            : contentRoot;
        var dir = Path.IsPathRooted(opts.Directory)
            ? opts.Directory
            : Path.Combine(root, opts.Directory);
        _writer = new DailyExceptionLogWriter(dir, opts.FileNameFormat);
        _categoryPrefix = string.Empty;
    }

    public ILogger CreateLogger(string categoryName) =>
        new DailyExceptionLogger(categoryName, _writer);

    public void Dispose() => _writer.Dispose();
}

internal sealed class DailyExceptionLogger : ILogger
{
    private readonly string _category;
    private readonly DailyExceptionLogWriter _writer;

    public DailyExceptionLogger(string category, DailyExceptionLogWriter writer)
    {
        _category = category;
        _writer = writer;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= LogLevel.Warning;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel < LogLevel.Warning)
            return;

        // Every Error+, plus any Warning that carries an Exception.
        if (exception is null && logLevel < LogLevel.Error)
            return;

        var message = formatter(state, exception);
        _writer.Write(logLevel, _category, message, exception);
    }
}

internal sealed class DailyExceptionLogWriter : IDisposable
{
    private readonly string _directory;
    private readonly string _fileNameFormat;
    private readonly object _gate = new();
    private bool _disposed;

    public DailyExceptionLogWriter(string directory, string fileNameFormat)
    {
        _directory = directory;
        _fileNameFormat = string.IsNullOrWhiteSpace(fileNameFormat)
            ? "exceptions-{0:yyyy-MM-dd}.txt"
            : fileNameFormat;
        Directory.CreateDirectory(_directory);
    }

    public void Write(LogLevel level, string category, string message, Exception? exception)
    {
        if (_disposed)
            return;

        var now = DateTime.Now;
        var path = Path.Combine(_directory, string.Format(_fileNameFormat, now));
        var block = BuildBlock(now, level, category, message, exception);

        lock (_gate)
        {
            File.AppendAllText(path, block);
        }
    }

    private static string BuildBlock(
        DateTime now,
        LogLevel level,
        string category,
        string message,
        Exception? exception)
    {
        var sb = new System.Text.StringBuilder(512);
        sb.AppendLine("============================================================");
        sb.AppendLine($"Time     : {now:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Level    : {level}");
        sb.AppendLine($"Category : {category}");
        if (!string.IsNullOrWhiteSpace(message))
            sb.AppendLine($"Message  : {message}");
        if (exception is not null)
        {
            sb.AppendLine("Exception:");
            sb.AppendLine(exception.ToString());
        }
        sb.AppendLine();
        return sb.ToString();
    }

    public void Dispose() => _disposed = true;
}
