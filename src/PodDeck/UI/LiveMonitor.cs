using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PodDeck.Core;

namespace PodDeck.UI;

/// <summary>
/// Real-time view of what PodDeck hears from the AirPods, for checking detection delay.
/// Press Space the moment you take a pod out; the window shows how long until it was detected.
/// </summary>
public sealed class LiveMonitor : Window
{
    static readonly Brush Bg = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16));
    static readonly Brush Fg = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
    static readonly Brush Muted = new SolidColorBrush(Color.FromArgb(0x99, 0xF5, 0xF5, 0xF7));
    static readonly Brush Tile = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));

    readonly TextBlock _left = Big(), _right = Big(), _lid = Small(), _raw = Small(), _age = Small(), _timer = Small();
    readonly Border _leftTile, _rightTile;
    readonly StackPanel _events = new();
    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly Stopwatch _sinceMark = new();
    DateTime _lastAdv = DateTime.MinValue;
    PodState? _last;

    public LiveMonitor()
    {
        Title = "PodDeck Live";
        Width = 380; SizeToContent = SizeToContent.Height;
        Topmost = true; ResizeMode = ResizeMode.NoResize;
        Background = Bg; Foreground = Fg;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        _leftTile = PodTile("LEFT", _left);
        _rightTile = PodTile("RIGHT", _right);
        var pods = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 10) };
        pods.Children.Add(_leftTile);
        pods.Children.Add(_rightTile);

        var hint = new TextBlock
        {
            Text = "Press SPACE the moment you take a pod out or put it back.\nThe delay until PodDeck notices shows below.",
            Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        };

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(pods);
        root.Children.Add(_lid);
        root.Children.Add(_age);
        root.Children.Add(_raw);
        root.Children.Add(new Border { Height = 1, Background = Tile, Margin = new Thickness(0, 10, 0, 10) });
        root.Children.Add(hint);
        root.Children.Add(_timer);
        root.Children.Add(new TextBlock { Text = "EVENTS", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 12, 0, 4) });
        root.Children.Add(_events);
        Content = root;

        _tick.Tick += (_, _) => RefreshClock();
        _tick.Start();
        KeyDown += OnKey;
        Closed += (_, _) => _tick.Stop();
        _timer.Text = "Timer: idle";
        Render();
    }

    static TextBlock Big() => new() { FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
    static TextBlock Small() => new() { FontSize = 12.5, Margin = new Thickness(0, 2, 0, 2), FontFamily = new FontFamily("Cascadia Mono, Consolas") };

    Border PodTile(string label, TextBlock value)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11 });
        sp.Children.Add(value);
        return new Border { Child = sp, Background = Tile, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 14, 12), Margin = new Thickness(3) };
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        _sinceMark.Restart();
        AddEvent("SPACE pressed, waiting for detection...", Muted);
        e.Handled = true;
    }

    /// <summary>Called on the UI thread for every advertisement.</summary>
    public void OnAdvert(PodState s)
    {
        _lastAdv = DateTime.Now;
        bool changed = _last is null || _last.LeftInEar != s.LeftInEar || _last.RightInEar != s.RightInEar || _last.LidOpen != s.LidOpen;
        if (_last is not null && changed)
        {
            string what = "";
            if (_last.LeftInEar != s.LeftInEar) what += s.LeftInEar ? "LEFT in  " : "LEFT out  ";
            if (_last.RightInEar != s.RightInEar) what += s.RightInEar ? "RIGHT in  " : "RIGHT out  ";
            if (_last.LidOpen != s.LidOpen) what += s.LidOpen ? "lid open  " : "lid closed  ";
            if (_sinceMark.IsRunning)
            {
                what += $"→ detected {_sinceMark.Elapsed.TotalSeconds:0.0} s after SPACE";
                _sinceMark.Reset();
            }
            AddEvent(what.Trim(), Fg);
        }
        _last = s;
        Render();
    }

    public void OnAction(string text) => AddEvent(text, Visuals.Green);

    void Render()
    {
        Paint(_left, _leftTile, _last?.LeftInEar);
        Paint(_right, _rightTile, _last?.RightInEar);
        _lid.Text = _last is null ? "Case: —" : $"Case: {(_last.InCase ? (_last.LidOpen ? "pods docked, lid OPEN" : "pods docked, lid closed") : "pods out of case")}";
        _raw.Text = _last is null ? "Waiting for AirPods..." : $"status=0x{_last.Status:X2}  lid=0x{_last.LidByte:X2}  rssi={_last.Rssi}";
    }

    static void Paint(TextBlock t, Border tile, bool? inEar)
    {
        t.Text = inEar is null ? "—" : inEar.Value ? "In ear" : "Out";
        tile.Background = inEar == true ? new SolidColorBrush(Color.FromArgb(0x55, 0x34, 0xC7, 0x59)) : Tile;
    }

    void RefreshClock()
    {
        _age.Text = _lastAdv == DateTime.MinValue ? "Last signal: never" : $"Last signal: {(DateTime.Now - _lastAdv).TotalSeconds:0.0} s ago";
        if (_sinceMark.IsRunning) _timer.Text = $"Timer: {_sinceMark.Elapsed.TotalSeconds:0.0} s since SPACE";
        else if (!_timer.Text.StartsWith("Timer: idle")) _timer.Text = "Timer: idle";
    }

    void AddEvent(string text, Brush color)
    {
        _events.Children.Insert(0, new TextBlock { Text = $"{DateTime.Now:HH:mm:ss.f}  {text}", Foreground = color, FontSize = 12, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Cascadia Mono, Consolas") });
        while (_events.Children.Count > 10) _events.Children.RemoveAt(_events.Children.Count - 1);
    }
}
