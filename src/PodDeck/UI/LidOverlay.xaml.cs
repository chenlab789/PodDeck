using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PodDeck.Core;

namespace PodDeck.UI;

/// <summary>Ink-theme card that pops up when the case lid opens.</summary>
public partial class LidOverlay : Window
{
    readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromSeconds(8) };
    public Func<int> LowLevel { get; set; } = () => 20;
    public event Action? ActionRequested;

    public LidOverlay()
    {
        InitializeComponent();
        _hide.Tick += (_, _) => { if (!IsMouseOver) FadeOut(); };
        MouseLeave += (_, _) => { _hide.Stop(); _hide.Start(); };
    }

    public void Update(PodState s, bool connected)
    {
        NameText.Text = s.ModelName;
        StatusText.Text = connected ? "Connected" : "Not connected";
        Dot.Fill = connected ? Visuals.Green : Visuals.Muted;
        ActionText.Text = connected ? "Hand off to iPhone" : "Use on this PC";
        int low = LowLevel();
        Visuals.FillBig(LeftCol, "Left", s.Left, s.LeftCharging, low);
        Visuals.FillBig(RightCol, "Right", s.Right, s.RightCharging, low);
        Visuals.FillBig(CaseCol, "Case", s.Case, s.CaseCharging, low);
    }

    public void Pop()
    {
        if (!IsVisible) Show();
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - ActualWidth) / 2;
        double restTop = wa.Bottom - ActualHeight + 6;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
        BeginAnimation(TopProperty, new DoubleAnimation(restTop + 40, restTop, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
        _hide.Stop(); _hide.Start();
    }

    public void FadeOut()
    {
        _hide.Stop();
        var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
        a.Completed += (_, _) => { if (Opacity == 0) Hide(); };
        BeginAnimation(OpacityProperty, a);
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e) => FadeOut();
    void ActionBtn_Click(object sender, RoutedEventArgs e) { ActionRequested?.Invoke(); FadeOut(); }
}
