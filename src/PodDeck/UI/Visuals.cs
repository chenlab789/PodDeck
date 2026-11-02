using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PodDeck.UI;

/// <summary>Ink theme palette and the small building blocks shared by every window.</summary>
static class Visuals
{
    public static readonly Brush Bg = Make(0x0B, 0x0B, 0x0C);
    public static readonly Brush Border = Make(0x22, 0x22, 0x22);
    public static readonly Brush Line = Make(0x1C, 0x1C, 0x1E);
    public static readonly Brush Fg = Make(0xF5, 0xF5, 0xF7);
    public static readonly Brush Muted = Make(0x8E, 0x8E, 0x93);
    public static readonly Brush SegOff = Make(0x2C, 0x2C, 0x2E);
    public static readonly Brush Green = Make(0x30, 0xD1, 0x58);
    public static readonly Brush Orange = Make(0xFF, 0x9F, 0x0A);

    public static readonly FontFamily Mono = new("Segoe UI Variable Display, Segoe UI");

    static Brush Make(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    public static Brush LevelBrush(int? level, int low) => level is not null && level <= low ? Orange : Fg;

    public static string Pct(int? level) => level is null ? "—" : $"{level}%";
    public static string Num(int? level) => level is null ? "—" : $"{level}";

    /// <summary>10-step segmented battery bar.</summary>
    public static StackPanel Segments(int? level, int low, double w = 5, double h = 4)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        int on = level is null ? 0 : (int)Math.Ceiling(level.Value / 10.0);
        var lit = LevelBrush(level, low);
        for (int i = 0; i < 10; i++)
            sp.Children.Add(new Rectangle { Width = w, Height = h, RadiusX = 1, RadiusY = 1, Margin = new Thickness(0, 0, 2, 0), Fill = i < on ? lit : SegOff });
        return sp;
    }

    /// <summary>Big light number + segment bar + small caps label, used by the lid card.</summary>
    public static void FillBig(Panel host, string caption, int? level, bool charging, int low)
    {
        host.Children.Clear();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = Num(level), FontSize = 30, FontWeight = FontWeights.Light, FontFamily = Mono,
            Foreground = LevelBrush(level, low),
        });
        if (charging)
            row.Children.Add(new Path
            {
                Data = Geometry.Parse("M6,0 L0,7 H4 L3,12 L9,5 H5 Z"), Fill = Green, Width = 7, Height = 11,
                Stretch = Stretch.Uniform, Margin = new Thickness(4, 0, 0, 6), VerticalAlignment = VerticalAlignment.Bottom,
            });
        host.Children.Add(row);
        var seg = Segments(level, low);
        seg.Margin = new Thickness(0, 4, 0, 4);
        host.Children.Add(seg);
        host.Children.Add(new TextBlock { Text = caption.ToUpperInvariant(), FontSize = 10.5, Foreground = Muted });
    }

    /// <summary>iOS-style switch drawn into an existing Border (track) + Ellipse (knob).</summary>
    public static void PaintSwitch(System.Windows.Controls.Border track, Ellipse knob, bool on)
    {
        track.Background = on ? Green : SegOff;
        knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }
}
