using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PodDeck.Core;

namespace PodDeck.UI;

/// <summary>Design A: light glass card that pops up when the case lid opens.</summary>
public partial class LidOverlay : Window
{
    static readonly Brush Text = new SolidColorBrush(Color.FromRgb(0x1D, 0x1D, 0x1F));
    static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73));
    static readonly Brush Track = new SolidColorBrush(Color.FromArgb(0x14, 0, 0, 0));

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
        StatusText.Text = connected ? "Connected" : "Not connected to this PC";
        ActionText.Text = connected ? "Hand off to iPhone" : "Use on this PC";
        int low = LowLevel();
        Visuals.FillRing(LeftCol, "Left", s.Left, s.LeftCharging, low, Text, Muted, Track);
        Visuals.FillRing(RightCol, "Right", s.Right, s.RightCharging, low, Text, Muted, Track);
        Visuals.FillRing(CaseCol, "Case", s.Case, s.CaseCharging, low, Text, Muted, Track);
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
