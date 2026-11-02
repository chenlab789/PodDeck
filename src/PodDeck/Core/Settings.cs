using System.IO;
using System.Text.Json;

namespace PodDeck.Core;

public sealed class Settings
{
    public bool PauseOnRemove { get; set; } = true;
    public bool KeepStereo { get; set; } = false;
    public bool ShowLidOverlay { get; set; } = true;
    public bool LowBatteryWarning { get; set; } = true;
    public int LowBatteryLevel { get; set; } = 20;
    public string DeviceName { get; set; } = "AirPods";

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
