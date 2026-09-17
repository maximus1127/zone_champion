using System.IO;

namespace ZoneChampion;

/// <summary>Minimal file log at %APPDATA%\ZoneChampion\zonechampion.log.</summary>
internal static class Log
{
    private const long MaxSize = 2 * 1024 * 1024;
    private static readonly object Gate = new();

    public static string FolderPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoneChampion");

    public static string FilePath { get; } = Path.Combine(FolderPath, "zonechampion.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxSize)
                {
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                }

                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level,-5} {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Logging must never take the app down.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
