using System;
using System.Drawing;
using System.IO;
using System.Threading;
using CodexBarWin.Core.Models;
using CodexBarWin.Tray;

internal static class RenewalTests
{
    public static void Run(string output, Action<bool, string> check)
    {
        UsageSnapshot State(string id, ProviderStatus status) => UsageSnapshot.ForState(id, id, status, "message");

        check(ClaudeRenewal.Applies(State("claude", ProviderStatus.Unavailable)), "Expired or rejected Claude login offers renewal");
        check(!ClaudeRenewal.Applies(State("claude", ProviderStatus.Error)), "Transient Claude errors do not offer renewal");
        check(!ClaudeRenewal.Applies(State("claude", ProviderStatus.Ok)) && !ClaudeRenewal.Applies(State("claude", ProviderStatus.NotInstalled)),
            "Live or uninstalled Claude does not offer renewal");
        check(!ClaudeRenewal.Applies(State("codex", ProviderStatus.Unavailable)), "Codex login problems never open Claude Code");

        // PATH entries must be absolute, so the fixtures are too.
        string root = Path.GetFullPath(Path.Combine(output, "renewal-" + Guid.NewGuid().ToString("N")));
        string bin = Path.Combine(root, "bin with space");
        string home = Path.Combine(root, "home");
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(home);
        check(ClaudeRenewal.FindCli(null, null) == null && ClaudeRenewal.FindCli("", home) == null, "Missing CLI is reported, not guessed");
        File.WriteAllText(Path.Combine(bin, "claude.cmd"), "@echo fake");
        check(ClaudeRenewal.FindCli($"\"{bin}\"{Path.PathSeparator}C:\\nowhere", home) == Path.Combine(bin, "claude.cmd"), "CLI found on PATH, including quoted entries");
        check(ClaudeRenewal.FindCli("bin with space", home) == null, "Relative PATH entries are never trusted");
        string native = Path.Combine(home, ".local", "bin");
        Directory.CreateDirectory(native);
        File.WriteAllText(Path.Combine(native, "claude.exe"), "fake");
        check(ClaudeRenewal.FindCli("C:\\nowhere", home) == Path.Combine(native, "claude.exe"), "Native installer location is the fallback");
        check(ClaudeRenewal.TerminalArguments(@"C:\Program Files\A&B\claude.exe") == "/k \"\"C:\\Program Files\\A&B\\claude.exe\"\"",
            "CLI path stays quoted for cmd.exe");

        var expired = WeeklyIconState.FromSnapshots(new[] { State("codex", ProviderStatus.Unavailable), State("claude", ProviderStatus.Unavailable) });
        check(expired.ClaudeNeedsLogin && expired.Claude == null && expired.Tooltip.Contains("Claude login needed"), "Tray tooltip says the Claude login needs renewal");
        check(!WeeklyIconState.FromSnapshots(new[] { State("claude", ProviderStatus.Error) }).ClaudeNeedsLogin, "Transient errors keep the normal tray state");
        using (var icon = TrayIconRenderer.Render(expired, 128))
        {
            var edge = icon.GetPixel(74, 40);
            bool Near(Color a, Color b) => Math.Abs(a.R - b.R) < 8 && Math.Abs(a.G - b.G) < 8 && Math.Abs(a.B - b.B) < 8;
            check(Near(edge, TrayIconRenderer.ExpiredColor) && !Near(edge, TrayIconRenderer.ClaudeColor), "Claude bar outline turns gray when the login needs renewal");
            icon.Save(Path.Combine(output, "claude-login-expired-128.png"));
        }

        string watched = Path.Combine(root, "watched");
        Directory.CreateDirectory(watched);
        int signals = 0;
        using (var signal = FileChangeSignal.TryCreate(watched, ".credentials.json", () => Interlocked.Increment(ref signals), TimeSpan.FromMilliseconds(200)))
        {
            check(signal != null, "Existing Claude folder can be watched");
            File.WriteAllText(Path.Combine(watched, "other.json"), "{}");
            Thread.Sleep(800);
            check(Volatile.Read(ref signals) == 0, "Other files do not trigger a refresh");
            for (int i = 0; i < 3; i++) File.WriteAllText(Path.Combine(watched, ".credentials.json"), "{\"n\":" + i + "}");
            check(WaitFor(() => Volatile.Read(ref signals) > 0), "Writing the login file triggers a refresh");
            Thread.Sleep(600);
            check(Volatile.Read(ref signals) == 1, "A burst of writes triggers one refresh");
            string temporary = Path.Combine(watched, "creds.tmp");
            File.WriteAllText(temporary, "{}");
            File.Move(temporary, Path.Combine(watched, ".credentials.json"), overwrite: true);
            check(WaitFor(() => Volatile.Read(ref signals) == 2), "Atomic replace of the login file triggers a refresh");
        }
        int afterDispose = Volatile.Read(ref signals);
        File.WriteAllText(Path.Combine(watched, ".credentials.json"), "{}");
        Thread.Sleep(600);
        check(Volatile.Read(ref signals) == afterDispose, "No refresh after the watch is disposed");
        check(FileChangeSignal.TryCreate(Path.Combine(root, "missing"), ".credentials.json", () => { }) == null, "Missing Claude folder is skipped, not an error");
    }

    private static bool WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 50; i++) { if (condition()) return true; Thread.Sleep(100); }
        return condition();
    }
}
