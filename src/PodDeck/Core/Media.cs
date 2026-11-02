using Windows.Media.Control;

namespace PodDeck.Core;

/// <summary>Pause / resume whatever is playing, via the system media session (SMTC).</summary>
public static class Media
{
    static GlobalSystemMediaTransportControlsSessionManager? _mgr;
    static bool _pausedByUs;

    static async Task<GlobalSystemMediaTransportControlsSession?> Session()
    {
        _mgr ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return _mgr.GetCurrentSession();
    }

    /// <summary>Pause if something is playing; remember that we did it.</summary>
    public static async Task PauseIfPlaying()
    {
        var s = await Session();
        if (s is null) return;
        var st = s.GetPlaybackInfo().PlaybackStatus;
        if (st == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            _pausedByUs = await s.TryPauseAsync();
        }
    }

    /// <summary>Resume only if we were the ones who paused.</summary>
    public static async Task ResumeIfWePaused()
    {
        if (!_pausedByUs) return;
        _pausedByUs = false;
        var s = await Session();
        if (s is not null) await s.TryPlayAsync();
    }
}
