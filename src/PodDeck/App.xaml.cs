using System.Windows;
using PodDeck.Core;
using PodDeck.UI;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace PodDeck;

public partial class App : Application
{
    Settings _settings = null!;
    PodWatcher _watcher = null!;
    LidOverlay _overlay = null!;
    TrayPanel _panel = null!;
    Hotkeys _hotkeys = null!;
    Forms.NotifyIcon _tray = null!;
    Mutex? _single;
    bool _lowWarned;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _single = new Mutex(true, "PodDeck.SingleInstance", out bool first);
        if (!first) { Shutdown(); return; }

        _settings = Settings.Load();
        BtAudio.NameMatch = _settings.DeviceName;

        if (e.Args.Contains("--diag"))
        {
            var log = new System.Text.StringBuilder();
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "poddeck-diag.txt");
            System.IO.File.WriteAllText(path, "started\n");
            BtAudio.Log = s => log.AppendLine(s);
            try
            {
                log.AppendLine($"connected={SafeIsConnected()}");
                using (var en = new NAudio.CoreAudioApi.MMDeviceEnumerator())
                    foreach (var d in en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.All, NAudio.CoreAudioApi.DeviceState.All))
                        try { log.AppendLine($"[all] {d.DataFlow} {d.State} '{d.FriendlyName}'"); } catch (Exception ex) { log.AppendLine($"[all] ? {ex.Message}"); }
                log.AppendLine($"Connect() => {BtAudio.Connect()}");
            }
            catch (Exception ex) { log.AppendLine("FATAL " + ex); }
            System.IO.File.WriteAllText(path, log.ToString());
            Shutdown();
            return;
        }

        _overlay = new LidOverlay { LowLevel = () => _settings.LowBatteryLevel };
        _overlay.ActionRequested += ToggleConnection;

        _panel = new TrayPanel(_settings);
        _panel.ConnectToggle += ToggleConnection;
        _panel.KeepStereoTurnedOn += ApplyKeepStereo;
        _panel.QuitRequested += Quit;

        _tray = new Forms.NotifyIcon { Icon = MakeTrayIcon(), Text = "PodDeck", Visible = true };
        _tray.MouseClick += (_, a) =>
        {
            if (a.Button != Forms.MouseButtons.Left) return;
            if (_panel.IsVisible || _panel.JustHidden) { _panel.Hide(); return; }
            _panel.Update(_watcher.Current, SafeIsConnected());
            _panel.ShowAtTray();
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show battery", null, (_, _) => ShowOverlay());
        menu.Items.Add("Quit PodDeck", null, (_, _) => Quit());
        _tray.ContextMenuStrip = menu;

        _hotkeys = new Hotkeys();
        _hotkeys.Register(Hotkeys.MOD_CONTROL | Hotkeys.MOD_ALT, 0x41 /* A */, ToggleConnection);
        _hotkeys.Register(Hotkeys.MOD_CONTROL | Hotkeys.MOD_ALT, 0x50 /* P */, ShowOverlay);

        _watcher = new PodWatcher();
        _watcher.StateChanged += s => Ui(() => OnState(s));
        _watcher.LidOpened += s => Ui(() => { if (_settings.ShowLidOverlay) { _overlay.Update(s, SafeIsConnected()); _overlay.Pop(); } });
        _watcher.LidClosed += () => Ui(() => _overlay.FadeOut());
        _watcher.EarCountChanged += (before, after) => Ui(() => OnEarChange(before, after));
        _watcher.Start();

        if (_settings.KeepStereo) ApplyKeepStereo();

        if (e.Args.Contains("--demo"))
        {
            var demo = new PodState { Model = 0x201B, ModelName = "AirPods 4", Left = 70, Right = 60, Case = 15, CaseCharging = true, InCase = true, SeenAt = DateTime.Now };
            _overlay.Update(demo, SafeIsConnected());
            _overlay.Pop();
            _panel.Update(demo, SafeIsConnected());
            _panel.ShowAtTray();
        }
    }

    void OnState(PodState s)
    {
        bool connected = SafeIsConnected();
        if (_overlay.IsVisible) _overlay.Update(s, connected);
        if (_panel.IsVisible) _panel.Update(s, connected);

        string line = $"L {Visuals.Pct(s.Left)}  R {Visuals.Pct(s.Right)}  Case {Visuals.Pct(s.Case)}";
        _tray.Text = line.Length > 63 ? line[..63] : line;

        int? lowest = new[] { s.Left, s.Right }.Where(v => v is not null && s.AnyInEar).Min();
        if (_settings.LowBatteryWarning && lowest is not null && lowest <= _settings.LowBatteryLevel)
        {
            if (!_lowWarned)
            {
                _lowWarned = true;
                _tray.ShowBalloonTip(4000, "AirPods battery low", line, Forms.ToolTipIcon.Warning);
            }
        }
        else if (lowest is not null && lowest > _settings.LowBatteryLevel + 10) _lowWarned = false;
    }

    async void OnEarChange(int before, int after)
    {
        bool connected = SafeIsConnected();
        Log.Write($"ear {before}->{after} pauseOnRemove={_settings.PauseOnRemove} connected={connected}");
        if (!_settings.PauseOnRemove || !connected) return;
        try
        {
            if (after < before) Log.Write("pause: " + await Media.PauseIfPlaying());
            else if (after > before) Log.Write("resume: " + await Media.ResumeIfWePaused());
        }
        catch (Exception ex) { Log.Write("media error " + ex.Message); }
    }

    void ShowOverlay()
    {
        var s = _watcher.Current;
        if (s is null)
        {
            _tray.ShowBalloonTip(3000, "PodDeck", "No AirPods nearby. Open the case near this PC.", Forms.ToolTipIcon.Info);
            return;
        }
        _overlay.Update(s, SafeIsConnected());
        _overlay.Pop();
    }

    void ToggleConnection()
    {
        bool connected = SafeIsConnected();
        bool ok;
        try { ok = connected ? BtAudio.Disconnect() : BtAudio.Connect(); }
        catch { ok = false; }

        string msg = !ok
            ? "Couldn't reach AirPods. Is it paired with this PC?"
            : connected ? "Disconnected. Tap AirPods on your iPhone to use it there." : "Connecting to this PC...";
        _tray.ShowBalloonTip(3000, "PodDeck", msg, ok ? Forms.ToolTipIcon.Info : Forms.ToolTipIcon.Warning);

        if (ok && !connected && _settings.KeepStereo)
            Task.Delay(4000).ContinueWith(_ => Ui(ApplyKeepStereo));
    }

    void ApplyKeepStereo()
    {
        try
        {
            var mic = BtAudio.KeepStereo();
            if (mic is null)
                _tray.ShowBalloonTip(3000, "Keep Stereo", "No other microphone found, AirPods mic stays default.", Forms.ToolTipIcon.Info);
        }
        catch { /* audio service busy */ }
    }

    static bool SafeIsConnected()
    {
        try { return BtAudio.IsConnected(); } catch { return false; }
    }

    void Ui(Action a) => Dispatcher.BeginInvoke(a);

    void Quit()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _hotkeys.Dispose();
        _watcher.Dispose();
        Shutdown();
    }

    static Drawing.Icon MakeTrayIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);
            using var b = new Drawing.SolidBrush(Drawing.Color.White);
            // Same glyph as the UI, scaled from the 44px grid.
            float k = 32f / 44f;
            void Pod(float cx, float stemX)
            {
                g.FillEllipse(b, (cx - 7) * k, 6 * k, 14 * k, 14 * k);
                using var path = RoundRect(stemX * k, 13 * k, 5 * k, 22 * k, 2.5f * k);
                g.FillPath(b, path);
            }
            Pod(14, 14);
            Pod(30, 25);
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    static Drawing.Drawing2D.GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        var p = new Drawing.Drawing2D.GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
