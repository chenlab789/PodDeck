using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PodDeck.Core;

namespace PodDeck.UI;

/// <summary>
/// Always-on-top battery strip docked to the right edge of the screen (Ink theme).
/// Click to open the tray panel; drag up/down to move it.
/// </summary>
public sealed class StatusHud : Window
{
    const double TubeH = 34;
    readonly Settings _settings;
    readonly Ellipse _dot = new() { Width = 6, Height = 6, Margin = new Thickness(0, 10, 0, 4) };
    readonly StackPanel _cols = new();
    Point _downAt;
    double _downTop;
    bool _dragging;

    public event Action? Clicked;

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
        ToolTip = "PodDeck · click to open, drag to move";

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new Path
        {
            Data = (Geometry)Application.Current.FindResource("PodsGlyph"),
            Fill = Visuals.Fg, Width = 18, Height = 18, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center,
        });
        _dot.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(_dot);
        stack.Children.Add(_cols);

        Content = new Border
        {
            Width = 52,
            Padding = new Thickness(0, 14, 0, 12),
            CornerRadius = new CornerRadius(16, 0, 0, 16),
            Background = Visuals.Bg,
            BorderBrush = Visuals.Border,
            BorderThickness = new Thickness(1, 1, 0, 1),
            Child = stack,
        };

        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        Update(null, false);
    }

    public void Update(PodState? s, bool connected)
    {
        _dot.Fill = connected ? Visuals.Green : Visuals.Muted;
        _cols.Children.Clear();
        int low = _settings.LowBatteryLevel;
        AddCol("L", s?.Left, low);
        AddCol("R", s?.Right, low);
        AddCol("C", s?.Case, low);
    }

    void AddCol(string label, int? level, int low)
    {
        var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        col.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = Visuals.Muted, HorizontalAlignment = HorizontalAlignment.Center });
        var tube = new Border
        {
            Width = 6, Height = TubeH, CornerRadius = new CornerRadius(3), Background = Visuals.SegOff,
            Margin = new Thickness(0, 3, 0, 3), HorizontalAlignment = HorizontalAlignment.Center, ClipToBounds = true,
        };
        tube.Child = new Border
        {
            Height = TubeH * (level ?? 0) / 100.0, VerticalAlignment = VerticalAlignment.Bottom,
            Background = Visuals.LevelBrush(level, low),
        };
        col.Children.Add(tube);
        col.Children.Add(new TextBlock
        {
            Text = Visuals.Num(level), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Visuals.LevelBrush(level, low),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        _cols.Children.Add(col);
    }

    public void Place()
    {
        if (!IsVisible) Show();
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth;
        Top = Math.Clamp(wa.Top + wa.Height * _settings.HudTop, wa.Top, wa.Bottom - ActualHeight);
    }

    void OnDown(object sender, MouseButtonEventArgs e)
    {
        _downAt = PointToScreen(e.GetPosition(this));
        _downTop = Top;
        _dragging = false;
        CaptureMouse();
    }

    void OnMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;
        var now = PointToScreen(e.GetPosition(this));
        // Screen points are physical pixels; convert the delta to WPF units.
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22 ?? 1;
        double dy = (now.Y - _downAt.Y) / scale;
        if (!_dragging && Math.Abs(dy) < 4) return;
        _dragging = true;
        var wa = SystemParameters.WorkArea;
        Top = Math.Clamp(_downTop + dy, wa.Top, wa.Bottom - ActualHeight);
    }

    void OnUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (_dragging)
        {
            var wa = SystemParameters.WorkArea;
            _settings.HudTop = (Top - wa.Top) / wa.Height;
            _settings.Save();
        }
        else Clicked?.Invoke();
    }
}
