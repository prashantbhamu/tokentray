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
    // cmd.exe expands this once and does not rescan the result, so '%', '&' or '^' in the path stay literal.
    internal const string CliVariable = "TOKENTRAY_CLAUDE_CLI";
    private static Process? _terminal;

    /// <summary>The provider says opening Claude Code can fix this login (expired, rejected or signed out).</summary>
    public static bool Applies(UsageSnapshot snapshot) => snapshot.ProviderId == "claude" && snapshot.LoginRenewable;

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

    /// <summary>
    /// TokenTray's own PATH is fixed when it starts at login; the saved user and machine PATH also include
    /// a Claude Code install made after that.
    /// </summary>
    internal static string CurrentPath()
    {
        string Read(EnvironmentVariableTarget target)
        {
            try { return Environment.ExpandEnvironmentVariables(Environment.GetEnvironmentVariable("PATH", target) ?? ""); }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return ""; }
        }
        return string.Join(Path.PathSeparator, Environment.GetEnvironmentVariable("PATH") ?? "",
            Read(EnvironmentVariableTarget.User), Read(EnvironmentVariableTarget.Machine));
    }

    /// <summary>Command line for cmd.exe. The doubled quotes keep a path with spaces intact.</summary>
    internal static string TerminalArguments() => $"/k \"\"%{CliVariable}%\"\"";

    internal static ProcessStartInfo StartInfo(string cli, string folder)
    {
        // Not shell-executed: TokenTray has no console, so cmd.exe gets a new visible console window of its own.
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), TerminalArguments())
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = folder
        };
        start.Environment[CliVariable] = cli;
        return start;
    }

    public static bool TryOpen(out string? error)
    {
        error = null;
        // While the terminal from the last click is still open, do not start a second CLI.
        if (IsRunning(_terminal)) return true;
        string? cli = FindCli(CurrentPath(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
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
            _terminal?.Dispose();
            _terminal = Process.Start(StartInfo(cli, folder));
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error = "Claude Code could not be opened: " + ex.Message;
            return false;
        }
    }

    private static bool IsRunning(Process? process)
    {
        try { return process != null && !process.HasExited; }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { return false; }
    }
}
