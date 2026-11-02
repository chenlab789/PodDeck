using System.IO;

namespace PodDeck.Core;

/// <summary>Tiny append-only log at %TEMP%\poddeck.log (truncated at 1 MB).</summary>
public static class Log
{
    static readonly string FilePath = Path.Combine(Path.GetTempPath(), "poddeck.log");
    static readonly object Gate = new();

    public static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 1_000_000) fi.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
        }
        catch { /* logging must never crash the app */ }
    }
}
