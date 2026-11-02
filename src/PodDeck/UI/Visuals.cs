using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PodDeck.UI;

/// <summary>Ink theme palette and the small building blocks shared by every window.</summary>
static class Visuals
{
    // Read live from Application resources so a theme switch takes effect on the next rebuild.
    public static Brush Bg => R("InkBg");
    public static Brush Border => R("InkBorder");
    public static Brush Line => R("InkLine");
    public static Brush Fg => R("InkFg");
    public static Brush Muted => R("InkMuted");
    public static Brush SegOff => R("InkSegOff");
    public static Brush Green => R("Green");
    public static Brush GreenText => R("GreenText");
    public static Brush Orange => R("Orange");

    public static readonly FontFamily Mono = new("Segoe UI Variable Display, Segoe UI");

    static Brush R(string key) => (Brush)Application.Current.Resources[key];

    public static Brush LevelBrush(int? level, int low) => level is not null && level <= low ? Orange : Fg;

    public static string Pct(int? level) => level is null ? "—" : $"{level}%";
    public static string Num(int? level) => level is null ? "—" : $"{level}";

    /// <summary>10-step segmented battery bar.</summary>
    public static StackPanel Segments(int? level, int low, double w = 5, double h = 4, bool stale = false)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        int on = level is null ? 0 : (int)Math.Ceiling(level.Value / 10.0);
        var lit = stale ? Muted : LevelBrush(level, low);
        for (int i = 0; i < 10; i++)
            sp.Children.Add(new Rectangle { Width = w, Height = h, RadiusX = 1, RadiusY = 1, Margin = new Thickness(0, 0, 2, 0), Fill = i < on ? lit : SegOff });
        return sp;
    }

    /// <summary>Big light number + segment bar + small caps label, used by the lid card.</summary>
    public static void FillBig(Panel host, string caption, int? level, bool charging, int low,
        bool stale = false, string? ago = null, string? tooltip = null)
    {
        host.Children.Clear();
        host.ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = Num(level), FontSize = 30, FontWeight = FontWeights.Light, FontFamily = Mono,
            Foreground = stale ? Muted : LevelBrush(level, low),
        });
        if (charging)
            row.Children.Add(new Path
            {
                Data = Geometry.Parse("M6,0 L0,7 H4 L3,12 L9,5 H5 Z"), Fill = Green, Width = 7, Height = 11,
                Stretch = Stretch.Uniform, Margin = new Thickness(4, 0, 0, 6), VerticalAlignment = VerticalAlignment.Bottom,
            });
        host.Children.Add(row);
        var seg = Segments(level, low, stale: stale);
        seg.Margin = new Thickness(0, 4, 0, 4);
        host.Children.Add(seg);
        string cap = caption.ToUpperInvariant() + (stale && !string.IsNullOrEmpty(ago) ? $" · {ago}" : "");
        host.Children.Add(new TextBlock { Text = cap, FontSize = 10.5, Foreground = Muted });
    }

    /// <summary>iOS-style switch drawn into an existing Border (track) + Ellipse (knob).</summary>
    public static void PaintSwitch(System.Windows.Controls.Border track, Ellipse knob, bool on)
    {
        track.Background = on ? Green : SegOff;
        knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }
}
