using System.Text.Json;

namespace DynamicIsland.Core;

public sealed record AppSettings(bool StartWithWindows = true, bool PlaySound = true, bool OnboardingDone = false)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Missing or corrupt file → defaults. Never throws.</summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }
}
