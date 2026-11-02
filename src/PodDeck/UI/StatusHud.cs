using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PodDeck.Core;

namespace PodDeck.UI;

/// <summary>
/// Always-on-top battery strip docked to a screen edge (Ink theme).
/// Right edge = vertical strip, top edge = horizontal strip.
/// Click to open the tray panel; drag anywhere and drop near an edge to dock there.
/// </summary>
public sealed class StatusHud : Window
{
    const double Tube = 34;
    readonly Settings _settings;
    readonly Border _frame = new();
    PodState? _state;
    bool _connected;

    public event Action? Clicked;

    bool Horizontal => _settings.HudEdge == "Top";

    public StatusHud(Settings settings)
    {
        _settings = settings;
        Title = "PodDeck HUD";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        Cursor = Cursors.Hand;
        ToolTip = "PodDeck · click to open, drag to an edge to move";

        _frame.Background = Visuals.Bg;
        _frame.BorderBrush = Visuals.Border;
        Content = _frame;

        MouseLeftButtonDown += OnDown;
        Update(null, false);
    }

    public void Update(PodState? s, bool connected)
    {
        _state = s;
        _connected = connected;
        Build();
    }

    void Build()
    {
        int low = _settings.LowBatteryLevel;
        var glyph = new Path
        {
            Data = (Geometry)Application.Current.FindResource("PodsGlyph"),
            Fill = Visuals.Fg, Width = 18, Height = 18, Stretch = Stretch.Uniform,
        };
        var dot = new Ellipse { Width = 6, Height = 6, Fill = _connected ? Visuals.Green : Visuals.Muted };

        var items = new (string Label, int? Level)[] { ("L", _state?.Left), ("R", _state?.Right), ("C", _state?.Case) };

        if (Horizontal)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            glyph.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(8, 0, 4, 0);
            dot.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(glyph);
            row.Children.Add(dot);
            foreach (var (label, level) in items) row.Children.Add(HItem(label, level, low));
            _frame.Child = row;
            _frame.Height = 38;
            _frame.Width = double.NaN;
            _frame.Padding = new Thickness(14, 0, 16, 0);
            _frame.CornerRadius = new CornerRadius(0, 0, 14, 14);
            _frame.BorderThickness = new Thickness(1, 0, 1, 1);
            _frame.HorizontalAlignment = HorizontalAlignment.Center;
            _frame.VerticalAlignment = VerticalAlignment.Top;
        }
        else
        {
            var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            dot.Margin = new Thickness(0, 10, 0, 4);
            dot.HorizontalAlignment = HorizontalAlignment.Center;
            col.Children.Add(glyph);
            col.Children.Add(dot);
            foreach (var (label, level) in items) col.Children.Add(VItem(label, level, low));
            _frame.Child = col;
            _frame.Width = 52;
            _frame.Height = double.NaN;
            _frame.Padding = new Thickness(0, 14, 0, 12);
            _frame.CornerRadius = new CornerRadius(16, 0, 0, 16);
            _frame.BorderThickness = new Thickness(1, 1, 0, 1);
            // Windows may make the window wider than the strip; hug its right side.
            _frame.HorizontalAlignment = HorizontalAlignment.Right;
            _frame.VerticalAlignment = VerticalAlignment.Top;
        }
    }

    static UIElement VItem(string label, int? level, int low)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = Visuals.Muted, HorizontalAlignment = HorizontalAlignment.Center });
        var tube = new Border
        {
            Width = 6, Height = Tube, CornerRadius = new CornerRadius(3), Background = Visuals.SegOff,
            Margin = new Thickness(0, 3, 0, 3), HorizontalAlignment = HorizontalAlignment.Center, ClipToBounds = true,
            Child = new Border { Height = Tube * (level ?? 0) / 100.0, VerticalAlignment = VerticalAlignment.Bottom, Background = Visuals.LevelBrush(level, low) },
        };
        sp.Children.Add(tube);
        sp.Children.Add(Number(level, low, HorizontalAlignment.Center));
        return sp;
    }

    static UIElement HItem(string label, int? level, int low)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 9.5, Foreground = Visuals.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
        var tube = new Border
        {
            Width = Tube, Height = 6, CornerRadius = new CornerRadius(3), Background = Visuals.SegOff,
            VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true,
            Child = new Border { Width = Tube * (level ?? 0) / 100.0, HorizontalAlignment = HorizontalAlignment.Left, Background = Visuals.LevelBrush(level, low) },
        };
        sp.Children.Add(tube);
        var n = Number(level, low, HorizontalAlignment.Left);
        n.Margin = new Thickness(6, 0, 0, 0);
        n.MinWidth = 18;
        n.VerticalAlignment = VerticalAlignment.Center;
        sp.Children.Add(n);
        return sp;
    }

    static TextBlock Number(int? level, int low, HorizontalAlignment align) => new()
    {
        Text = Visuals.Num(level), FontSize = 11, FontWeight = FontWeights.SemiBold,
        Foreground = Visuals.LevelBrush(level, low), HorizontalAlignment = align,
    };

    /// <summary>Show and snap to the saved edge and position.</summary>
    public void Place()
    {
        if (!IsVisible) Show();
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        if (Horizontal)
        {
            Top = wa.Top;
            Left = Math.Clamp(wa.Left + wa.Width * _settings.HudPos, wa.Left, wa.Right - ActualWidth);
        }
        else
        {
            Left = wa.Right - ActualWidth;
            Top = Math.Clamp(wa.Top + wa.Height * _settings.HudPos, wa.Top, wa.Bottom - ActualHeight);
        }
    }

    void OnDown(object sender, MouseButtonEventArgs e)
    {
        double left = Left, top = Top;
        // Windows' own move loop: smooth and never loses the pointer. Blocks until release.
        try { DragMove(); } catch (InvalidOperationException) { return; }
        bool moved = Math.Abs(Left - left) > 4 || Math.Abs(Top - top) > 4;
        if (!moved) { Left = left; Top = top; Clicked?.Invoke(); return; }
        Snap();
    }

    void Snap()
    {

        // Dock to whichever edge the strip was dropped closer to.
        var wa = SystemParameters.WorkArea;
        double toRight = wa.Right - (Left + ActualWidth);
        double toTop = Top - wa.Top;
        bool top = toTop < toRight;
        _settings.HudEdge = top ? "Top" : "Right";
        _settings.HudPos = top
            ? Math.Clamp((Left - wa.Left) / wa.Width, 0, 1)
            : Math.Clamp((Top - wa.Top) / wa.Height, 0, 1);
        _settings.Save();
        Build();
        UpdateLayout();
        Place();
    }
}
