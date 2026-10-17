using System;
using System.IO;
using System.Text.Json;

namespace CodexBarWin.Tray;

internal static class SettingsStore
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public static string FilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexBarWin", "settings.json");

	public static AppSettings Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
			}
		}
		catch
		{
		}
		return new AppSettings();
	}

	public static void Save(AppSettings settings)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
		}
		catch
		{
		}
	}
}
