using Windows.Media.Control;

namespace PodDeck.Core;

/// <summary>Pause / resume whatever is playing, via the system media sessions (SMTC).</summary>
public static class Media
{
    static GlobalSystemMediaTransportControlsSessionManager? _mgr;
    static readonly List<string> _pausedByUs = new();

    static async Task<IReadOnlyList<GlobalSystemMediaTransportControlsSession>> Sessions()
    {
        _mgr ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return _mgr.GetSessions();
    }

    /// <summary>Pause every session that is playing (not only the "current" one). Returns a summary for the log.</summary>
    public static async Task<string> PauseIfPlaying()
    {
        var sessions = await Sessions();
        var notes = new List<string>();
        foreach (var s in sessions)
        {
            var st = s.GetPlaybackInfo().PlaybackStatus;
            if (st == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                bool ok = await s.TryPauseAsync();
                notes.Add($"{s.SourceAppUserModelId} playing → pause {(ok ? "OK" : "REFUSED")}");
                if (ok) _pausedByUs.Add(s.SourceAppUserModelId);
            }
            else notes.Add($"{s.SourceAppUserModelId}={st}");
        }
        return sessions.Count == 0 ? "no media sessions" : string.Join(", ", notes);
    }

    /// <summary>Resume only the sessions we paused.</summary>
    public static async Task<string> ResumeIfWePaused()
    {
        if (_pausedByUs.Count == 0) return "nothing paused by us";
        var ids = _pausedByUs.ToList();
        _pausedByUs.Clear();
        foreach (var s in await Sessions())
            if (ids.Contains(s.SourceAppUserModelId)) await s.TryPlayAsync();
        return "resumed " + string.Join(", ", ids);
    }
}
