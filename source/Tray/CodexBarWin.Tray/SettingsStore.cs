using System;
using System.IO;
using System.Text.Json;

namespace CodexBarWin.Tray;

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string FilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexBarWin", "settings.json");

    public static AppSettings Load(string? filePath = null)
    {
        try
        {
            string path = filePath ?? FilePath;
            return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings() : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public static bool TrySave(AppSettings settings, out string? error, string? filePath = null)
    {
        error = null;
        string path = Path.GetFullPath(filePath ?? FilePath);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var normalized = settings.Clone();
            normalized.RefreshIntervalSeconds = normalized.ClampedIntervalSeconds();
            File.WriteAllText(temporary, JsonSerializer.Serialize(normalized, Options));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
