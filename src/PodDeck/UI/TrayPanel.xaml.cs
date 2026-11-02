using System.Windows;
using System.Windows.Controls;
using PodDeck.Core;

namespace PodDeck.UI;

/// <summary>Ink-theme panel opened from the tray icon.</summary>
public partial class TrayPanel : Window
{
    readonly Settings _settings;
    DateTime _hiddenAt;
    double _vol;

    public event Action? ConnectToggle;
    public event Action? KeepStereoToggleRequested;
    public event Action? HudToggled;
    public event Action? QuitRequested;

    public TrayPanel(Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        Deactivated += (_, _) => { Hide(); _hiddenAt = DateTime.Now; };
        RefreshToggles();

        // Press anywhere on the bar and drag, like Control Center.
        VolBar.MouseLeftButtonDown += (_, e) => { VolBar.CaptureMouse(); SetVolFromPointer(e); e.Handled = true; };
        VolBar.MouseMove += (_, e) => { if (VolBar.IsMouseCaptured) SetVolFromPointer(e); };
        VolBar.MouseLeftButtonUp += (_, e) => { VolBar.ReleaseMouseCapture(); e.Handled = true; };
        VolBar.MouseWheel += (_, e) => { SetVol(_vol + Math.Sign(e.Delta) * 5, true); e.Handled = true; };
        VolBar.SizeChanged += (_, _) => PaintVol();
    }

    /// <summary>Clicking the tray icon while the panel is open deactivates it first; don't reopen it.</summary>
    public bool JustHidden => DateTime.Now - _hiddenAt < TimeSpan.FromMilliseconds(300);

    public void Update(PodState? s, bool connected)
    {
        NameText.Text = s?.ModelName ?? _settings.DeviceName;
        ConnText.Text = connected ? "Connected" : "Not connected";
        ConnText.Foreground = connected ? Visuals.Green : Visuals.Muted;
        ConnBtnText.Text = connected ? "Hand off to iPhone" : "Use on this PC";

        BatRow.Children.Clear();
        int low = _settings.LowBatteryLevel;
        AddBat(0, "Left", s?.Left, s?.LeftCharging == true, low);
        AddBat(1, "Right", s?.Right, s?.RightCharging == true, low);
        AddBat(2, "Case", s?.Case, s?.CaseCharging == true, low);

        var v = BtAudio.GetVolume();
        VolBar.IsEnabled = v is not null;
        VolBar.Opacity = v is null ? 0.4 : 1;
        SetVol((v ?? 0) * 100, false);
        if (v is null) VolText.Text = "—";
    }

    void AddBat(int col, string label, int? level, bool charging, int low)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Visuals.Muted });
        sp.Children.Add(new TextBlock
        {
            Text = (charging ? "+" : "") + Visuals.Pct(level), FontSize = 15, FontWeight = FontWeights.Light,
            FontFamily = Visuals.Mono, Foreground = Visuals.LevelBrush(level, low), Margin = new Thickness(0, 2, 0, 4),
        });
        sp.Children.Add(Visuals.Segments(level, low, 4, 3));
        Grid.SetColumn(sp, col);
        BatRow.Children.Add(sp);
    }

    void RefreshToggles()
    {
        Visuals.PaintSwitch(PauseSw, PauseKnob, _settings.PauseOnRemove);
        Visuals.PaintSwitch(HudSw, HudKnob, _settings.ShowStatusHud);
        Visuals.PaintSwitch(StereoSw, StereoKnob, _settings.KeepStereo);
    }

    public void Refresh() => RefreshToggles();

    public void ShowAtTray()
    {
        Show();
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth + 10;
        Top = wa.Bottom - ActualHeight + 10;
        Activate();
    }

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

    void PaintVol() => VolFill.Width = VolBar.ActualWidth * _vol / 100;

    void PauseRow_Click(object sender, RoutedEventArgs e)
    {
        _settings.PauseOnRemove = !_settings.PauseOnRemove;
        _settings.Save();
        RefreshToggles();
    }

    void HudRow_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowStatusHud = !_settings.ShowStatusHud;
        _settings.Save();
        RefreshToggles();
        HudToggled?.Invoke();
    }

    /// <summary>The app decides whether Keep Stereo can turn on (it needs a real second mic).</summary>
    void StereoRow_Click(object sender, RoutedEventArgs e)
    {
        KeepStereoToggleRequested?.Invoke();
        RefreshToggles();
    }

    void ConnBtn_Click(object sender, RoutedEventArgs e) => ConnectToggle?.Invoke();
    void Quit_Click(object sender, RoutedEventArgs e) => QuitRequested?.Invoke();
}
