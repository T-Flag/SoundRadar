using System.IO;
using System.Text.Json;

namespace SoundRadar.Models;

public class AdaptiveThresholdConfig
{
    public double AdaptationTimeSec { get; set; } = 0.5;
    public double TriggerFactor { get; set; } = 1.5;
}

public class FrequencyBandsConfig
{
    public double[] SubBass { get; set; } = [20, 80];
    public double[] LowMid { get; set; } = [80, 400];
    public double[] Mid { get; set; } = [400, 1800];
    public double[] HighMid { get; set; } = [1800, 6000];
    public double NoiseFloorDb { get; set; } = -60;
    public double CeilingDb { get; set; } = 0;
}

public class SurroundConfig
{
    public bool Enabled { get; set; } = true;
    public Dictionary<string, float> ChannelAngles { get; set; } = new()
    {
        ["FL"] = -45, ["FR"] = 45, ["FC"] = 0,
        ["SL"] = -90, ["SR"] = 90,
        ["RL"] = -135, ["RR"] = 135,
    };
}

public class AppConfig
{
    public float IntensityThreshold { get; set; } = 0.010f;
    public float MaxExpectedPan { get; set; } = 0.25f;
    public bool OverlayVisible { get; set; } = true;
    public AdaptiveThresholdConfig AdaptiveThreshold { get; set; } = new();
    public FrequencyBandsConfig FrequencyBands { get; set; } = new();
    public SurroundConfig Surround { get; set; } = new();
    public bool SpectrumDisplayVisible { get; set; } = false;
    public bool DebugVisible { get; set; } = true;

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
