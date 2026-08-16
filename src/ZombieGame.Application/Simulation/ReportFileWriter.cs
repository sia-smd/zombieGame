namespace ZombieGame.Application.Simulation;

using System.Text;

public static class ReportFileWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static Task WriteUtf8Async(string path, string content, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, content, Utf8WithBom, cancellationToken);

    public static void WriteUtf8(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, Utf8WithBom);
        File.Move(tempPath, path, overwrite: true);
    }
}
