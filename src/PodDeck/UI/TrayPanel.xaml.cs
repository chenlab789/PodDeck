using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PodDeck.Core;

namespace PodDeck.UI;

/// <summary>Design C: dark Control-Center-style panel opened from the tray icon.</summary>
public partial class TrayPanel : Window
{
    static readonly Brush Fg = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
    static readonly Brush Muted = new SolidColorBrush(Color.FromArgb(0x99, 0xF5, 0xF5, 0xF7));
    static readonly Brush Off = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
    static readonly Brush OffDot = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));

    readonly Settings _settings;
    DateTime _hiddenAt;

    public event Action? ConnectToggle;
    public event Action? KeepStereoToggleRequested;
    public event Action? QuitRequested;

    public TrayPanel(Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        Deactivated += (_, _) => { Hide(); _hiddenAt = DateTime.Now; };
        BuildModes();
        RefreshToggles();

        // Plain bar instead of a Slider: press anywhere and drag, like Control Center.
        VolBar.MouseLeftButtonDown += (_, e) => { VolBar.CaptureMouse(); SetVolFromPointer(e); e.Handled = true; };
        VolBar.MouseMove += (_, e) => { if (VolBar.IsMouseCaptured) SetVolFromPointer(e); };
        VolBar.MouseLeftButtonUp += (_, e) => { VolBar.ReleaseMouseCapture(); e.Handled = true; };
        VolBar.MouseWheel += (_, e) => { SetVol(_vol + Math.Sign(e.Delta) * 5, true); e.Handled = true; };
        VolBar.SizeChanged += (_, _) => PaintVol();
    }

    double _vol;

    void SetVolFromPointer(System.Windows.Input.MouseEventArgs e)
    {
        double x = e.GetPosition(VolBar).X;
        SetVol(Math.Clamp(x / Math.Max(1, VolBar.ActualWidth), 0, 1) * 100, true);
    }

    void SetVol(double v, bool apply)
    {
        _vol = Math.Clamp(v, 0, 100);
        PaintVol();
        VolText.Text = $"{Math.Round(_vol)}";
        if (apply && VolBar.IsEnabled) BtAudio.SetVolume((float)(_vol / 100));
    }

    void PaintVol()
    {
        double w = VolBar.ActualWidth;
        // Keep at least a full pill end visible so the fill reads as rounded, like iOS.
        VolFill.Width = _vol <= 0 ? 0 : Math.Max(26, w * _vol / 100);
    }

    /// <summary>Clicking the tray icon while the panel is open deactivates it first; don't reopen it.</summary>
    public bool JustHidden => DateTime.Now - _hiddenAt < TimeSpan.FromMilliseconds(300);

    void BuildModes()
    {
        foreach (var (label, filled) in new[] { ("Off", false), ("Transparency", false), ("Adaptive", true), ("Noise Cancel", true) })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 6, 8, 6) };
            var dot = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = Off, Margin = new Thickness(0, 0, 10, 0) };
            dot.Child = new Ellipse { Width = 10, Height = 10, Stroke = Fg, StrokeThickness = 1.6, Fill = filled ? Fg : Brushes.Transparent };
            row.Children.Add(dot);
            row.Children.Add(new TextBlock { Text = label, Foreground = Fg, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
            Modes.Children.Add(row);
        }
        Modes.Opacity = 0.45;
    }

    public void Update(PodState? s, bool connected)
    {
        NameText.Text = s?.ModelName ?? _settings.DeviceName;
        ConnText.Text = connected ? "Connected" : "Not connected";
        ConnBtnText.Text = connected ? "Hand off to iPhone" : "Use on this PC";

        Bars.Children.Clear();
        int low = _settings.LowBatteryLevel;
        if (s is null)
        {
            Bars.Children.Add(new TextBlock { Text = "Open the case near this PC to read battery", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            AddBar("Left", s.Left, s.LeftCharging, low, s.LeftInEar);
            AddBar("Right", s.Right, s.RightCharging, low, s.RightInEar);
            AddBar("Case", s.Case, s.CaseCharging, low, false);
        }

        var v = BtAudio.GetVolume();
        VolBar.IsEnabled = v is not null;
        VolBar.Opacity = v is null ? 0.4 : 1;
        SetVol((v ?? 0) * 100, false);
        if (v is null) VolText.Text = "—";
    }

    void AddBar(string label, int? level, bool charging, int low, bool inEar)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });

        var name = new TextBlock { Text = label, Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        if (inEar) name.Foreground = Fg;
        g.Children.Add(name);

        var track = new Border { Height = 5, CornerRadius = new CornerRadius(3), Background = Off, VerticalAlignment = VerticalAlignment.Center };
        var fill = new Border
        {
            Height = 5, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left,
            Background = Visuals.LevelBrush(level, low),
        };
        track.Child = fill;
        track.SizeChanged += (_, e) => fill.Width = e.NewSize.Width * (level ?? 0) / 100.0;
        Grid.SetColumn(track, 1);
        g.Children.Add(track);

        var pct = new TextBlock
        {
            Text = (charging ? "+" : "") + Visuals.Pct(level), Foreground = Fg, FontSize = 11, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(pct, 2);
        g.Children.Add(pct);
        Bars.Children.Add(g);
    }

    void RefreshToggles()
    {
        Paint(PauseDot, PauseDotIn, PauseState, _settings.PauseOnRemove);
        Paint(StereoDot, StereoDotIn, StereoState, _settings.KeepStereo);
    }

    static void Paint(Border dot, Ellipse inner, TextBlock state, bool on)
    {
        dot.Background = on ? Visuals.Green : Off;
        inner.Fill = on ? Brushes.White : OffDot;
        state.Text = on ? "On" : "Off";
    }

    public void ShowAtTray()
    {
        Show();
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth + 10;
        Top = wa.Bottom - ActualHeight + 10;
        Activate();
    }

    void PauseTile_Click(object sender, RoutedEventArgs e)
    {
        _settings.PauseOnRemove = !_settings.PauseOnRemove;
        _settings.Save();
        RefreshToggles();
    }

    /// <summary>The app decides whether Keep Stereo can turn on (it needs a real second mic).</summary>
    void StereoTile_Click(object sender, RoutedEventArgs e)
    {
        KeepStereoToggleRequested?.Invoke();
        RefreshToggles();
    }

    public void Refresh() => RefreshToggles();

    void ConnBtn_Click(object sender, RoutedEventArgs e) => ConnectToggle?.Invoke();
    void Quit_Click(object sender, RoutedEventArgs e) => QuitRequested?.Invoke();
}
