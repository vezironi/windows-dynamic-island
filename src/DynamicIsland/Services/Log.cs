using System.IO;

namespace DynamicIsland.Services;

/// <summary>Error log at %LOCALAPPDATA%\DynamicIsland\debug.log (capped at ~1 MB).</summary>
internal static class Log
{
    private static readonly object Gate = new();
    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DynamicIsland");
    private static readonly string FilePath = Path.Combine(Dir, "debug.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1_000_000) File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch { /* logging must never crash the app */ }
    }

    public static void Error(string where, Exception ex) => Write($"ERROR {where}: {ex.GetType().Name}: {ex.Message}");
}
