using System.IO;
using System.Text.Json;

namespace PodDeck.Core;

public sealed class Settings
{
    public bool PauseOnRemove { get; set; } = true;
    public bool KeepStereo { get; set; } = false;
    public bool ShowLidOverlay { get; set; } = true;
    public bool ShowStatusHud { get; set; } = true;
    /// <summary>"Dark", "Light" or "Auto" (follow Windows app theme).</summary>
    public string Theme { get; set; } = "Dark";
    /// <summary>Which screen edge the HUD docks to: "Right" (vertical) or "Top" (horizontal).</summary>
    public string HudEdge { get; set; } = "Right";
    /// <summary>HUD position along its edge, as a fraction of the work area (user drags it).</summary>
    public double HudPos { get; set; } = 0.32;
    public bool LowBatteryWarning { get; set; } = true;
    public int LowBatteryLevel { get; set; } = 20;
    public string DeviceName { get; set; } = "AirPods";
    /// <summary>Default audio endpoints from before Keep Stereo was turned on.</summary>
    public Dictionary<string, string>? DefaultsBeforeKeepStereo { get; set; }

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodDeck");
    static string FilePath => Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* corrupt file: fall back to defaults */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opts));
        }
        catch { /* not fatal */ }
    }
}
