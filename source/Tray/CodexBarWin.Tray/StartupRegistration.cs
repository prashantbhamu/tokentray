using System;
using Microsoft.Win32;

namespace CodexBarWin.Tray;

internal static class StartupRegistration
{
	private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "CodexBarWin";

	public static bool IsEnabled()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: false);
			return registryKey?.GetValue("CodexBarWin") is string;
		}
		catch
		{
			return false;
		}
	}

	public static bool Set(bool enabled)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true) ?? Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
			if (registryKey == null)
			{
				return IsEnabled();
			}
			if (enabled)
			{
				string processPath = Environment.ProcessPath;
				if (string.IsNullOrEmpty(processPath))
				{
					return false;
				}
				registryKey.SetValue("CodexBarWin", "\"" + processPath + "\"");
			}
			else
			{
				registryKey.DeleteValue("CodexBarWin", throwOnMissingValue: false);
			}
			return enabled;
		}
		catch
		{
			return IsEnabled();
		}
	}
}
