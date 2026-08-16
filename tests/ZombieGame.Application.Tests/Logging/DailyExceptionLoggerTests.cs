namespace ZombieGame.Application.Tests.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Infrastructure.Logging;

public class DailyExceptionLoggerTests
{
    [Fact]
    public void Logger_WritesExceptionToDailyFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zg-logs-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = new DailyExceptionLoggerProvider(
                Options.Create(new DailyExceptionLogOptions
                {
                    Directory = dir,
                    FileNameFormat = "exceptions-{0:yyyy-MM-dd}.txt"
                }));

            var logger = provider.CreateLogger("Test.Category");
            logger.LogError(new InvalidOperationException("boom"), "failed doing {Action}", "test");

            var files = Directory.GetFiles(dir, "exceptions-*.txt");
            Assert.Single(files);
            var text = File.ReadAllText(files[0]);
            Assert.Contains("InvalidOperationException", text);
            Assert.Contains("boom", text);
            Assert.Contains("failed doing test", text);
            Assert.Contains("Test.Category", text);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Logger_SkipsInfoWithoutException()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zg-logs-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = new DailyExceptionLoggerProvider(
                Options.Create(new DailyExceptionLogOptions { Directory = dir }));

            var logger = provider.CreateLogger("Test");
            logger.LogInformation("hello");

            Assert.False(Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
