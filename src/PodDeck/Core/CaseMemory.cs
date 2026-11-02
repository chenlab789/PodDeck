namespace PodDeck.Core;

/// <summary>
/// AirPods only report the case battery while a pod sits in the case. Remember the last
/// reported value so the UI can show it (dimmed, with its age) the rest of the time.
/// </summary>
public static class CaseMemory
{
    static readonly TimeSpan MaxAge = TimeSpan.FromDays(3);
    static Settings? _settings;

    public readonly record struct CaseInfo(int? Level, bool Stale, DateTime? SeenAt)
    {
        public string Ago => SeenAt is null ? "" : Format(DateTime.Now - SeenAt.Value);
        public string Tooltip => SeenAt is null ? "" : $"Last seen {SeenAt.Value:HH:mm}" +
            (SeenAt.Value.Date == DateTime.Today ? "" : $" on {SeenAt.Value:d MMM}");
    }

    public static void Init(Settings settings) => _settings = settings;

    /// <summary>Record a live case reading. Saves only when the value changes or every 10 minutes.</summary>
    public static void Observe(PodState s)
    {
        if (_settings is null || s.Case is null) return;
        bool changed = _settings.LastCase != s.Case;
        bool old = _settings.LastCaseAt is null || DateTime.Now - _settings.LastCaseAt.Value > TimeSpan.FromMinutes(10);
        _settings.LastCase = s.Case;
        _settings.LastCaseAt = DateTime.Now;
        if (changed || old) _settings.Save();
    }

    /// <summary>Live value if the AirPods are reporting it now, else the remembered one (stale), else unknown.</summary>
    public static CaseInfo Resolve(PodState? s)
    {
        if (s?.Case is not null) return new CaseInfo(s.Case, false, null);
        if (_settings?.LastCase is int lvl && _settings.LastCaseAt is DateTime at && DateTime.Now - at < MaxAge)
            return new CaseInfo(lvl, true, at);
        return new CaseInfo(null, false, null);
    }

    static string Format(TimeSpan t) =>
        t.TotalMinutes < 1 ? "now" :
        t.TotalHours < 1 ? $"{(int)t.TotalMinutes}m ago" :
        t.TotalDays < 1 ? $"{(int)t.TotalHours}h ago" :
        $"{(int)t.TotalDays}d ago";
}
