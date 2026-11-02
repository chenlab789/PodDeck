using System.IO;
using System.Text.Json;

namespace PodDeck.Core;

public sealed class Settings
{
    public bool PauseOnRemove { get; set; } = true;
    public bool KeepStereo { get; set; } = false;
    public bool ShowLidOverlay { get; set; } = true;
    public bool ShowStatusHud { get; set; } = true;
    /// <summary>HUD vertical position as a fraction of the work area height (user can drag it).</summary>
    public double HudTop { get; set; } = 0.32;
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
