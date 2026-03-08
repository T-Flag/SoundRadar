using System.IO;
using System.Text.Json;

namespace SoundRadar.Models;

public class AppConfig
{
    public float IntensityThreshold { get; set; } = 0.010f;
    public float MaxExpectedPan { get; set; } = 0.25f;
    public bool OverlayVisible { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string GetConfigPath()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(dir, "config.json");
    }

    public static AppConfig Load()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
        {
            var defaults = new AppConfig();
            defaults.Save();
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            var path = GetConfigPath();
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Silently ignore write errors
        }
    }
}
