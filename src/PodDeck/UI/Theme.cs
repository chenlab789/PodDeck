using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace PodDeck.UI;

/// <summary>
/// Ink theme in dark and light. Colors live in Application resources (InkBg, InkFg, ...) so
/// XAML picks them up through DynamicResource; code-built visuals read them via <see cref="Visuals"/>.
/// </summary>
static class Theme
{
    public static readonly string[] Modes = { "Dark", "Light", "Auto" };

    public static event Action? Changed;
    public static bool IsLight { get; private set; }

    static string _mode = "Dark";

    public static void Init(string mode)
    {
        _mode = Modes.Contains(mode) ? mode : "Dark";
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (_mode == "Auto" && e.Category == UserPreferenceCategory.General)
                Application.Current.Dispatcher.BeginInvoke(Apply);
        };
        Apply();
    }

    public static void SetMode(string mode)
    {
        _mode = mode;
        Apply();
    }

    static bool WindowsUsesLight()
    {
        try
        {
            var v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0);
            return v is int i && i == 1;
        }
        catch { return false; }
    }

    static void Apply()
    {
        IsLight = _mode == "Light" || (_mode == "Auto" && WindowsUsesLight());
        var r = Application.Current.Resources;
        if (IsLight)
        {
            Set(r, "InkBg", 0xFF, 0xFF, 0xFF);
            Set(r, "InkBorder", 0xE0, 0xE0, 0xE5);
            Set(r, "InkLine", 0xEE, 0xEE, 0xF2);
            Set(r, "InkFg", 0x1D, 0x1D, 0x1F);
            Set(r, "InkMuted", 0x6E, 0x6E, 0x73);
            Set(r, "InkOutline", 0xD1, 0xD1, 0xD6);
            Set(r, "InkSegOff", 0xE5, 0xE5, 0xEA);
            Set(r, "Green", 0x34, 0xC7, 0x59);
            Set(r, "GreenText", 0x24, 0x8A, 0x3D);   // darker so text passes contrast on white
            Set(r, "Orange", 0xC9, 0x34, 0x00);
            r["ShadowOpacity"] = 0.16;
        }
        else
        {
            Set(r, "InkBg", 0x0B, 0x0B, 0x0C);
            Set(r, "InkBorder", 0x22, 0x22, 0x22);
            Set(r, "InkLine", 0x1C, 0x1C, 0x1E);
            Set(r, "InkFg", 0xF5, 0xF5, 0xF7);
            Set(r, "InkMuted", 0x8E, 0x8E, 0x93);
            Set(r, "InkOutline", 0x2C, 0x2C, 0x2E);
            Set(r, "InkSegOff", 0x2C, 0x2C, 0x2E);
            Set(r, "Green", 0x30, 0xD1, 0x58);
            Set(r, "GreenText", 0x30, 0xD1, 0x58);
            Set(r, "Orange", 0xFF, 0x9F, 0x0A);
            r["ShadowOpacity"] = 0.45;
        }
        Changed?.Invoke();
    }

    static void Set(ResourceDictionary r, string key, byte red, byte green, byte blue)
    {
        var b = new SolidColorBrush(Color.FromRgb(red, green, blue));
        b.Freeze();
        r[key] = b;
    }
}
