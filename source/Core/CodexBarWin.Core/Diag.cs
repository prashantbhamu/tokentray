using System;
using System.IO;

namespace CodexBarWin.Core;

public static class Diag
{
    private static readonly bool Enabled;

    private static readonly string LogPath;

    public static void Log(string message)
    {
        if (!Enabled)
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    static Diag()
    {
        string environmentVariable = Environment.GetEnvironmentVariable("CODEXBARWIN_DEBUG");
        bool enabled = ((environmentVariable == "1" || environmentVariable == "true") ? true : false);
        Enabled = enabled;
        LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexBarWin", "debug.log");
    }
}
