using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

internal static class Diagnostics
{
    public static bool TryRun(string[] args)
    {
        foreach (string command in new[] { "--status", "--shot", "--shot-settings" })
        {
            int index = Array.IndexOf(args, command);
            if (index < 0) continue;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            { Environment.ExitCode = 2; return true; }
            string path = args[index + 1];
            try
            {
                if (command == "--status")
                {
                    // Diagnostics follow the same network/provider settings as the live widget.
                    var snapshots = ProviderFactory.Create(SettingsStore.Load()).RefreshAll();
                    File.WriteAllText(path, JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true }));
                }
                else if (command == "--shot-settings")
                { using var settings = new SettingsForm(SettingsStore.Load()); Capture(settings, path); }
                else
                {
                    var snapshots = Array.IndexOf(args, "--sample-data") >= 0 ? SampleSnapshots() : ProviderFactory.Create(SettingsStore.Load()).RefreshAll();
                    using var popup = new PopupForm(() => { });
                    popup.Render(snapshots);
                    if (Array.IndexOf(args, "--preview-light") >= 0) popup.PreviewTheme(true);
                    if (Array.IndexOf(args, "--preview-dark") >= 0) popup.PreviewTheme(false);
                    Capture(popup, path);
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                try { File.WriteAllText(path + ".err", ex.ToString()); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return true;
        }
        return false;
    }

    private static void Capture(Form form, string path)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-4000, -4000);
        form.Show();
        Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, ImageFormat.Png);
        form.Close();
    }

    internal static IReadOnlyList<UsageSnapshot> SampleSnapshots()
    {
        var now = DateTimeOffset.UtcNow;
        return new[] {
            new UsageSnapshot("codex", "Codex", "Prolite", new[] { new Meter("Weekly",40,100,"%") },
                new[] { new ResetWindow("Weekly",now.AddDays(4).AddHours(16).AddSeconds(5)) },ProviderStatus.Ok,null,now),
            new UsageSnapshot("claude", "Claude Code", "pro", new[] { new Meter("Session (5h)",20,100,"%"),new Meter("Weekly",19,100,"%") },
                new[] { new ResetWindow("Session (5h)",now.AddMinutes(16).AddSeconds(5)),new ResetWindow("Weekly",now.AddDays(5).AddHours(6).AddSeconds(5)) },ProviderStatus.Ok,null,now)
        };
    }
}
