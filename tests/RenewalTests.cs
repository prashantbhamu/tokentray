using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using CodexBarWin.Core.Models;
using CodexBarWin.Core.Providers;
using CodexBarWin.Tray;

internal static class RenewalTests
{
    // Every credential written here is fake.
    public static void Run(string output, Action<bool, string> check)
    {
        UsageSnapshot State(string id, ProviderStatus status, bool renewable = false) =>
            UsageSnapshot.ForState(id, id, status, "message") with { LoginRenewable = renewable };

        check(ClaudeRenewal.Applies(State("claude", ProviderStatus.Unavailable, renewable: true)), "Renewable Claude login offers renewal");
        check(!ClaudeRenewal.Applies(State("claude", ProviderStatus.Unavailable)), "Login problems Claude Code cannot fix do not offer renewal");
        check(!ClaudeRenewal.Applies(State("claude", ProviderStatus.Error)) && !ClaudeRenewal.Applies(State("claude", ProviderStatus.Ok)),
            "Transient errors and live usage do not offer renewal");
        check(!ClaudeRenewal.Applies(State("codex", ProviderStatus.Unavailable, renewable: true)), "Codex login problems never open Claude Code");

        // PATH entries must be absolute, so the fixtures are too.
        string root = Path.GetFullPath(Path.Combine(output, "renewal-" + Guid.NewGuid().ToString("N")));
        RunProvider(root, check);

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
        check(ClaudeRenewal.CurrentPath().Contains(Environment.GetEnvironmentVariable("PATH") ?? "", StringComparison.Ordinal),
            "Saved PATH extends, and never replaces, TokenTray's own PATH");

        // Run the real cmd.exe command line against a fake CLI whose path has a space, '&' and '%'.
        string odd = Path.Combine(root, "odd 100%done & more");
        Directory.CreateDirectory(odd);
        string marker = Path.Combine(odd, "ran.txt");
        string fakeCli = Path.Combine(odd, "claude.cmd");
        File.WriteAllText(fakeCli, "@echo ran> \"%~dp0ran.txt\"\r\n");
        var start = ClaudeRenewal.StartInfo(fakeCli, odd);
        check(!start.UseShellExecute && start.Arguments.StartsWith("/k ", StringComparison.Ordinal) && !start.Arguments.Contains(fakeCli),
            "CLI path is passed through the environment, not the command line");
        start.Arguments = "/c" + start.Arguments[2..];
        start.CreateNoWindow = true;
        using (var process = Process.Start(start)!)
        {
            check(process.WaitForExit(15000) && File.Exists(marker), "cmd.exe runs a CLI path with spaces, '&' and '%' unchanged");
        }

        var expired = WeeklyIconState.FromSnapshots(new[] { State("codex", ProviderStatus.Unavailable), State("claude", ProviderStatus.Unavailable, renewable: true) });
        check(expired.ClaudeNeedsLogin && expired.Claude == null && expired.Tooltip.Contains("Claude login needed"), "Tray tooltip says the Claude login needs renewal");
        check(!WeeklyIconState.FromSnapshots(new[] { State("claude", ProviderStatus.Unavailable) }).ClaudeNeedsLogin, "Unfixable login problems keep the normal tray state");
        using (var icon = TrayIconRenderer.Render(expired, 128))
        {
            var edge = icon.GetPixel(74, 40);
            bool Near(Color a, Color b) => Math.Abs(a.R - b.R) < 8 && Math.Abs(a.G - b.G) < 8 && Math.Abs(a.B - b.B) < 8;
            check(Near(edge, TrayIconRenderer.ExpiredColor) && !Near(edge, TrayIconRenderer.ClaudeColor), "Claude bar outline turns gray when the login needs renewal");
            icon.Save(Path.Combine(output, "claude-login-expired-128.png"));
        }

        RunWatcher(root, check);
    }

    private static void RunProvider(string root, Action<bool, string> check)
    {
        string claudeDir = Path.Combine(root, "claude");
        string store = Path.Combine(root, "store");
        Directory.CreateDirectory(claudeDir);
        void Login(DateTimeOffset expires, string scopes) => File.WriteAllText(Path.Combine(claudeDir, ".credentials.json"),
            "{\"claudeAiOauth\":{\"accessToken\":\"fake-access\",\"refreshToken\":\"fake-refresh\",\"expiresAt\":" + expires.ToUnixTimeMilliseconds() +
            ",\"scopes\":[" + scopes + "],\"subscriptionType\":\"pro\"}}");
        var rejecting = new RejectingClient();
        UsageSnapshot Refresh(IClaudeUsageClient? client) => new ClaudeProvider(client, claudeDir, store).Refresh();

        var signedOut = Refresh(rejecting);
        check(signedOut.Status == ProviderStatus.Unavailable && signedOut.LoginRenewable, "Signed-out Claude Code is renewable by opening it");
        Login(DateTimeOffset.UtcNow.AddHours(-1), "\"user:profile\",\"user:inference\"");
        var expired = Refresh(rejecting);
        check(expired.Status == ProviderStatus.Unavailable && expired.LoginRenewable && rejecting.Calls == 0, "Expired login is renewable and makes no network call");
        Login(DateTimeOffset.UtcNow.AddHours(1), "\"user:inference\"");
        var noScope = Refresh(rejecting);
        check(noScope.Status == ProviderStatus.Unavailable && !noScope.LoginRenewable, "Missing user:profile scope does not offer renewal");
        Login(DateTimeOffset.UtcNow.AddHours(1), "\"user:profile\"");
        var rejected = Refresh(rejecting);
        check(rejected.Status == ProviderStatus.Unavailable && rejected.LoginRenewable && rejecting.Calls == 1, "Login rejected by the usage endpoint is renewable");
        check(!Refresh(null).LoginRenewable, "Network-off state never offers renewal");
        File.Delete(Path.Combine(claudeDir, ".credentials.json"));
        check(Refresh(null).Status == ProviderStatus.Unavailable && !Refresh(null).LoginRenewable, "Network-off without a login never offers renewal");
    }

    private static void RunWatcher(string root, Action<bool, string> check)
    {
        // A long debounce and generous waits keep these checks stable on a slow machine.
        var delay = TimeSpan.FromMilliseconds(500);
        string watched = Path.Combine(root, "watched");
        Directory.CreateDirectory(watched);
        int signals = 0;
        using (var signal = FileChangeSignal.TryCreate(watched, ".credentials.json", () => Interlocked.Increment(ref signals), delay))
        {
            check(signal != null && !signal.Faulted, "Existing Claude folder can be watched");
            File.WriteAllText(Path.Combine(watched, "other.json"), "{}");
            Thread.Sleep(1500);
            check(Volatile.Read(ref signals) == 0, "Other files do not trigger a refresh");
            for (int i = 0; i < 3; i++) File.WriteAllText(Path.Combine(watched, ".credentials.json"), "{\"n\":" + i + "}");
            check(WaitFor(() => Volatile.Read(ref signals) > 0), "Writing the login file triggers a refresh");
            Thread.Sleep(1500);
            check(Volatile.Read(ref signals) == 1, "A burst of writes triggers one refresh");
            string temporary = Path.Combine(watched, "creds.tmp");
            File.WriteAllText(temporary, "{}");
            File.Move(temporary, Path.Combine(watched, ".credentials.json"), overwrite: true);
            check(WaitFor(() => Volatile.Read(ref signals) == 2), "Atomic replace of the login file triggers a refresh");
        }
        int afterDispose = Volatile.Read(ref signals);
        File.WriteAllText(Path.Combine(watched, ".credentials.json"), "{}");
        Thread.Sleep(1500);
        check(Volatile.Read(ref signals) == afterDispose, "No refresh after the watch is disposed");
        check(FileChangeSignal.TryCreate(Path.Combine(root, "missing"), ".credentials.json", () => { }) == null, "Missing Claude folder is skipped, not an error");
    }

    private static bool WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 100; i++) { if (condition()) return true; Thread.Sleep(100); }
        return condition();
    }

    private sealed class RejectingClient : IClaudeUsageClient
    {
        public int Calls;
        public ClaudeUsageResponse Fetch(string accessToken)
        {
            Calls++;
            throw new ClaudeFetchException(ClaudeFetchErrorKind.Unauthorized, "fake 401");
        }
    }
}
