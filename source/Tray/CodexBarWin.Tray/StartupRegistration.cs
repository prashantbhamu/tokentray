using System;
using Microsoft.Win32;

namespace CodexBarWin.Tray;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexBarWin";

    internal static bool MatchesExecutable(string? command, string? executable) =>
        !string.IsNullOrWhiteSpace(executable) &&
        (string.Equals(command?.Trim(), $"\"{executable}\"", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(command?.Trim(), executable, StringComparison.OrdinalIgnoreCase));

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return MatchesExecutable(key?.GetValue(ValueName) as string, Environment.ProcessPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { return false; }
    }

    // Return the verified actual state, not the requested state, if the write fails.
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled && !string.IsNullOrWhiteSpace(Environment.ProcessPath)) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else if (!enabled) key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { }
        return IsEnabled();
    }
}
