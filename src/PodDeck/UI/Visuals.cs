using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PodDeck.UI;

static class Visuals
{
    public static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59)));
    public static readonly Brush Orange = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)));

    static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public static Brush LevelBrush(int? level, int low) => level is not null && level <= low ? Orange : Green;

    public static string Pct(int? level) => level is null ? "—" : $"{level}%";

    /// <summary>Battery ring with the percentage in the middle and a caption below.</summary>
    public static void FillRing(Panel host, string caption, int? level, bool charging, int low, Brush text, Brush muted, Brush track)
    {
        host.Children.Clear();
        const double size = 68, thick = 5;
        double r = (size - thick) / 2;
        double circumference = 2 * Math.PI * r;
        double frac = (level ?? 0) / 100.0;

        var g = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center };
        g.Children.Add(new Ellipse { Stroke = track, StrokeThickness = thick });
        if (level is not null && frac > 0)
        {
            g.Children.Add(new Ellipse
            {
                Stroke = LevelBrush(level, low),
                StrokeThickness = thick,
                StrokeDashCap = PenLineCap.Round,
                // Dash units are multiples of stroke thickness.
                StrokeDashArray = new DoubleCollection { circumference * frac / thick, 1000 },
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-90),
            });
        }
        var label = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (charging)
            label.Children.Add(new Path
            {
                Data = Geometry.Parse("M6,0 L0,7 H4 L3,12 L9,5 H5 Z"),
                Fill = Green, Width = 7, Height = 10, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        label.Children.Add(new TextBlock { Text = Pct(level), FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = text });
        g.Children.Add(label);

        host.Children.Add(g);
        host.Children.Add(new TextBlock { Text = caption, FontSize = 12, Foreground = muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
    }
}
