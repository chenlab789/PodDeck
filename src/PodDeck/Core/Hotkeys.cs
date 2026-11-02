using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PodDeck.Core;

/// <summary>System-wide hotkeys through a hidden message window.</summary>
public sealed class Hotkeys : IDisposable
{
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
    const int WM_HOTKEY = 0x0312;

    readonly HwndSource _src;
    readonly Dictionary<int, Action> _actions = new();
    int _next = 1;

    public Hotkeys()
    {
        _src = new HwndSource(new HwndSourceParameters("PodDeckHotkeys") { Width = 0, Height = 0, WindowStyle = 0 });
        _src.AddHook(Hook);
    }

    /// <summary>Returns false if another app already owns the combination.</summary>
    public bool Register(uint mods, uint vk, Action action)
    {
        int id = _next++;
        if (!RegisterHotKey(_src.Handle, id, mods | MOD_NOREPEAT, vk)) return false;
        _actions[id] = action;
        return true;
    }

    IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var a))
        {
            a();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_src.Handle, id);
        _src.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
