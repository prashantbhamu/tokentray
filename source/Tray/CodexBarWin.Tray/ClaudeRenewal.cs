using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

/// <summary>
/// Opens the official Claude Code CLI in a visible terminal so that the CLI renews its own login.
/// TokenTray never refreshes Claude tokens itself and never answers the CLI's prompts.
/// </summary>
internal static class ClaudeRenewal
{
    private static readonly TimeSpan RepeatGuard = TimeSpan.FromSeconds(15);
    private static DateTime _lastLaunch = DateTime.MinValue;

    /// <summary>A Claude login problem (expired, rejected or missing) that opening Claude Code can fix.</summary>
    public static bool Applies(UsageSnapshot snapshot) =>
        snapshot.ProviderId == "claude" && snapshot.Status == ProviderStatus.Unavailable;

    /// <summary>Finds the Claude Code CLI on PATH, or in the native installer's default folder.</summary>
    internal static string? FindCli(string? path, string? home)
    {
        foreach (string entry in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string directory = entry.Trim('"');
            // A relative PATH entry would resolve against TokenTray's working folder, so it is never trusted.
            if (!Path.IsPathFullyQualified(directory)) continue;
            foreach (string extension in new[] { ".exe", ".cmd", ".bat" })
            {
                string candidate = Path.Combine(directory, "claude" + extension);
                if (File.Exists(candidate)) return candidate;
            }
        }
        if (string.IsNullOrEmpty(home)) return null;
        string native = Path.Combine(home, ".local", "bin", "claude.exe");
        return File.Exists(native) ? native : null;
    }

    /// <summary>Command line for cmd.exe; the doubled quotes keep a path with spaces or symbols intact.</summary>
    internal static string TerminalArguments(string cli) => $"/k \"\"{cli}\"\"";

    public static bool TryOpen(out string? error)
    {
        error = null;
        // A second click while the first terminal is starting must not open another CLI.
        if (DateTime.UtcNow - _lastLaunch < RepeatGuard) return true;
        string? cli = FindCli(Environment.GetEnvironmentVariable("PATH"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (cli == null)
        {
            error = "Claude Code was not found. Install the Claude Code CLI, or run claude in a terminal to renew the login.";
            return false;
        }
        try
        {
            // An empty folder of its own: Claude Code reads no project files, and any trust prompt is the user's to answer.
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexBarWin", "claude-renew");
            Directory.CreateDirectory(folder);
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), TerminalArguments(cli))
            {
                UseShellExecute = true,
                WorkingDirectory = folder
            };
            using var process = Process.Start(start);
            _lastLaunch = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error = "Claude Code could not be opened: " + ex.Message;
            return false;
        }
    }
}
