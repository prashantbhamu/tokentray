using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CodexBarWin.Core.Models;
using CodexBarWin.Tray;

Application.SetHighDpiMode(HighDpiMode.SystemAware);
Application.EnableVisualStyles();
int count = 0;
void Check(bool condition, string description) { if (!condition) throw new Exception(description); count++; }
UsageSnapshot Snapshot(string id, double weekly, double session = 0, ProviderStatus status = ProviderStatus.Ok) =>
    new(id, id, null, id == "codex" ? new[] { new Meter("Weekly", weekly, 100, "%") }
        : new[] { new Meter("Session (5h)", session, 100, "%"), new Meter("Weekly", weekly, 100, "%") },
        Array.Empty<ResetWindow>(), status, null, DateTimeOffset.UtcNow);
WeeklyIconState State(double c, double a, double session = 0) => WeeklyIconState.FromSnapshots(new[] { Snapshot("codex", c), Snapshot("claude", a, session) });

var codex = State(40, 19, 95);
Check(codex.Codex == 0.4 && codex.Claude == 0.19, "Session usage must not override weekly");
Check(State(40, 60).Claude == 0.6, "Claude keeps its independent weekly percentage");
Check(State(40, 40).Codex == State(40, 40).Claude, "Equal weekly values remain independent");
Check(State(0, 0).Codex == 0 && State(0, 0).Claude == 0, "Zero is valid usage");
Check(State(150, -5).Codex == 1 && State(150, -5).Claude == 0, "Clamp provider out-of-range percentages");
Check(State(double.NaN, 19).Codex == null && State(double.NaN, 19).Claude == 0.19, "Ignore invalid percentages");
var missing = WeeklyIconState.FromSnapshots(Array.Empty<UsageSnapshot>());
Check(missing.Codex == null && missing.Claude == null, "No data must not fabricate zero");
Check(WeeklyIconState.FromSnapshots(new[] { Snapshot("codex", 80, 95, ProviderStatus.Error), Snapshot("claude", 19) }).Codex == null, "Unavailable provider must not fabricate usage");
var sessionOnly = new UsageSnapshot("claude", "Claude", null, new[] { new Meter("Session (5h)", 99, 100, "%") }, Array.Empty<ResetWindow>(), ProviderStatus.Ok, null, DateTimeOffset.UtcNow);
Check(WeeklyIconState.FromSnapshots(new[] { sessionOnly }).Claude == null, "No fallback from weekly to session");
Check(State(40, 19).Tooltip.Contains("Codex 40%") && State(40, 19).Tooltip.Contains("Claude 19%"), "Tooltip exposes both weekly values");
Check(!State(40, 19).Tooltip.Contains("Weekly", StringComparison.OrdinalIgnoreCase), "Tooltip omits Weekly prefix");

var output = args.Length > 0 ? args[0] : "test-results";
Directory.CreateDirectory(output);
ProviderRiskTests.Run(output, Check);
ProviderRiskTests.RunReviewFixes(output, Check);
ProviderRiskTests.RunCacheIntegration(output, Check);
RenewalTests.Run(output, Check);
foreach (var workArea in new[] { new Rectangle(0, 0, 1920, 1032), new Rectangle(0, 0, 1920, 1020), new Rectangle(-1920, 0, 1920, 1032), new Rectangle(0, -1080, 1920, 1032) })
{
    foreach (var size in new[] { new Size(284, 293), new Size(284, 450) })
    {
        var location = PopupPlacement.BottomRight(workArea, size);
        Check(workArea.Contains(new Rectangle(location, size)), "Popup remains inside work area, including secondary monitors and error resize");
    }
}
var cache = new UsageCache();
cache.Apply(Snapshot("codex", 44));
var cached = cache.Apply(Snapshot("codex", 0, 0, ProviderStatus.Error));
Check(cache.IsStale && cached.Meters.Single().Used == 44 && cached.Message!.Contains("showing"), "Transient failures preserve and label last good values");
cache.Apply(Snapshot("codex", 45));
Check(!cache.IsStale, "Successful refresh clears stale flag");
cache.Apply(Snapshot("codex", 0, 0, ProviderStatus.NoData));
Check(cache.Apply(Snapshot("codex", 0, 0, ProviderStatus.Error)).Status == ProviderStatus.Error, "No-data reset does not resurrect old usage");
cache.Clear();
Check(!cache.IsStale, "Clearing cache resets stale state");
Check(!ProviderFactory.NetworkAllowed(true, "true") && !ProviderFactory.NetworkAllowed(true, "TRUE") && !ProviderFactory.NetworkAllowed(true, "1"), "Network override honored case-insensitively");
Check(!ProviderFactory.NetworkAllowed(false, null) && ProviderFactory.NetworkAllowed(true, null), "Explicit provider network setting honored");
Check(Equals(typeof(ProviderFactory).GetMethod(nameof(ProviderFactory.Create))!.GetParameters()[1].DefaultValue, false), "Normal provider construction defaults to production (read-only Claude login)");
Check(StartupRegistration.MatchesExecutable("\"C:\\Apps\\CodexBarWin.exe\"", "C:\\Apps\\CodexBarWin.exe"), "Quoted startup path recognized");
Check(!StartupRegistration.MatchesExecutable("\"C:\\Other\\CodexBarWin.exe\"", "C:\\Apps\\CodexBarWin.exe"), "Stale startup entry is not mistaken for this installation");
string settingsDirectory = Path.Combine(output, "settings-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(settingsDirectory);
string settingsPath = Path.Combine(settingsDirectory, "settings.json");
Check(SettingsStore.TrySave(new AppSettings { RefreshIntervalSeconds = 1, ClaudeEnabled = false }, out _, settingsPath), "Settings save succeeds atomically");
Check(SettingsStore.Load(settingsPath).RefreshIntervalSeconds == 30 && !SettingsStore.Load(settingsPath).ClaudeEnabled, "Settings round-trip and interval clamp");
Check(SettingsStore.TrySave(new AppSettings { RefreshIntervalSeconds = 99999 }, out _, settingsPath) && SettingsStore.Load(settingsPath).RefreshIntervalSeconds == 3600, "Atomic overwrite preserves valid bounded settings");
Check(Directory.GetFiles(settingsDirectory, "*.tmp").Length == 0, "No temporary settings files left after save");
Check(!SettingsStore.TrySave(new AppSettings(), out string? saveError, settingsDirectory) && saveError != null, "Write failures reported instead of silently ignored");
File.WriteAllText(settingsPath, "invalid JSON");
Check(SettingsStore.Load(settingsPath).RefreshIntervalSeconds == 120, "Invalid settings recover safely to defaults");
foreach (var (name, state, color) in new[] { ("codex", codex, TrayIconRenderer.CodexColor), ("claude", State(40, 60), TrayIconRenderer.ClaudeColor), ("hundred", State(100, 20), TrayIconRenderer.CodexColor) })
{
    foreach (int size in new[] { 16, 20, 24, 32, 128 })
    {
        using var bitmap = TrayIconRenderer.Render(state, size);
        Check(bitmap.Width == size && bitmap.Height == size, "Exact native render size");
        int transparentPixels = 0;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) if (bitmap.GetPixel(x, y).A < 16) transparentPixels++;
        // Tiny resampled glyph/rail edges carry partial alpha; test truly empty
        // area rather than demanding transparent pixels at an antialiased rail corner.
        Check(transparentPixels > size * size / 16, $"Empty icon area has no background tile at {size}px ({transparentPixels} clear pixels)");
        Check(bitmap.GetPixel(size / 2, size / 2).A < 32, "Gap stays clear apart from tiny-size antialias fringe");
        Check(bitmap.GetPixel(0, 0).A == 0, "No backing tile at outside corner");
        if (size == 128)
        {
            Check(bitmap.GetPixel(64, 64).A == 0, "No numeral in hollow transparent center");
        }
        bitmap.Save(Path.Combine(output, $"{name}-{size}.png"), ImageFormat.Png);
    }
}
using (var meters = TrayIconRenderer.Render(State(40, 19), 128))
{
    bool Close(Color a, Color b) => Math.Abs(a.R - b.R) < 8 && Math.Abs(a.G - b.G) < 8 && Math.Abs(a.B - b.B) < 8;
    Check(Close(meters.GetPixel(34, 110), TrayIconRenderer.CodexColor), "Left bar fills upward in Codex black");
    Check(Close(meters.GetPixel(94, 110), TrayIconRenderer.ClaudeColor), "Right bar fills upward in Claude orange");
    Check(!Close(meters.GetPixel(34, 30), TrayIconRenderer.CodexColor), "Unused left bar remains gray");
    Check(!Close(meters.GetPixel(94, 30), TrayIconRenderer.ClaudeColor), "Unused right bar remains gray");
    Check(Close(meters.GetPixel(14, 110), TrayIconRenderer.CodexColor), "Thicker left bar extends toward edge");
    Check(Close(meters.GetPixel(114, 110), TrayIconRenderer.ClaudeColor), "Thicker right bar extends toward edge");
    Check(meters.GetPixel(64, 110).A == 0, "Narrow gap still separates the bars");
    Check(Close(meters.GetPixel(10, 40), TrayIconRenderer.CodexColor), "Black border encloses gray Codex track");
    Check(Close(meters.GetPixel(74, 40), TrayIconRenderer.ClaudeColor), "Orange border encloses gray Claude track");
    Check(Close(meters.GetPixel(34, 6), TrayIconRenderer.CodexColor), "Codex border includes top edge");
    Check(Close(meters.GetPixel(94, 6), TrayIconRenderer.ClaudeColor), "Claude border includes top edge");
}
using (var zero = TrayIconRenderer.Render(State(0, 0), 128))
{
    Check(zero.GetPixel(10, 40).R < 30, "Zero-usage Codex keeps its outline");
    Check(zero.GetPixel(74, 40).R > 220 && zero.GetPixel(74, 40).G < 140, "Zero-usage Claude keeps its outline");
    Check(zero.GetPixel(34, 40).R > 200 && zero.GetPixel(94, 40).G > 200, "Zero-usage interiors remain gray");
}
Exception? popupFailure = null;
var ui = new Thread(() =>
{
    try
    {
        int refreshes = 0, renewals = 0;
        using var popup = new PopupForm(() => refreshes++, () => renewals++);
        popup.Location = new Point(-4000, -4000);
        _ = popup.Handle;
        Console.WriteLine($"Display check: DPI={popup.DeviceDpi}; screen={Screen.PrimaryScreen?.Bounds}; work area={Screen.PrimaryScreen?.WorkingArea}; tray={SystemInformation.SmallIconSize}.");
        using (var body = new Font("Segoe UI", 13.5f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            Check(TextRenderer.MeasureText("Weekly · resets in 4d 16h", body, Size.Empty, TextFormatFlags.NoPadding).Width <= 170,
                "Reset text fits the compact popup at current display DPI");
        }
        Check(!popup.ShowInTaskbar && popup.Opacity == 1, "Popup stays opaque and off taskbar");
        popup.Render(new[] { Snapshot("codex", 40), Snapshot("claude", 19, 20) });
        Check(popup.Width == 284 && popup.Height < 340, $"Circular popup remains compact ({popup.Width}x{popup.Height})");
        popup.Show();
        Application.DoEvents();
        var refresh = popup.Controls.OfType<Button>().Single(b => b.AccessibleName == "Refresh usage");
        var renew = popup.Controls.OfType<Button>().Single(b => b != refresh);
        Check(!renew.Visible, "Renew button hidden while Claude usage is live");
        refresh.PerformClick();
        Check(refreshes == 1, "Refresh icon invokes refresh callback");
        popup.SetRefreshing(true);
        refresh.PerformClick();
        Check(!refresh.Enabled && refreshes == 1, "Refresh disabled while fetching");
        popup.SetRefreshing(false);
        refresh.PerformClick();
        Check(refreshes == 2, "Refresh reenabled after fetch");
        foreach (bool light in new[] { true, false })
        {
            popup.PreviewTheme(light);
            using var shot = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size));
            shot.Save(Path.Combine(output, light ? "popup-light.png" : "popup-dark.png"));
            bool HasColor(Color c) { int n = 0; for (int y = 15; y < shot.Height - 30; y++) for (int x = 18; x < 82; x++) { var p = shot.GetPixel(x, y); if (Math.Abs(p.R - c.R) < 5 && Math.Abs(p.G - c.G) < 5 && Math.Abs(p.B - c.B) < 5) n++; } return n > 30; }
            Check(HasColor(TrayIconRenderer.CodexColor), "Popup ring uses Codex black");
            Check(HasColor(TrayIconRenderer.ClaudeColor), "Popup rings use Claude orange");
        }
        popup.Render(Array.Empty<UsageSnapshot>(), true);
        Check(popup.Height == 84 && !refresh.Enabled, "Loading state is compact and noninteractive");
        popup.Render(new[] { Snapshot("codex", 40, 0, ProviderStatus.Error), Snapshot("claude", 19) });
        Check(popup.Height > 100 && popup.Height < 340, "Provider error retains a usable popup");
        using var errorShot = new Bitmap(popup.Width, popup.Height);
        popup.DrawToBitmap(errorShot, new Rectangle(Point.Empty, errorShot.Size));
        errorShot.Save(Path.Combine(output, "popup-error.png"));
        var longError = Snapshot("codex", 0, 0, ProviderStatus.Error) with { Message = new string('x', 9000) };
        popup.Render(new[] { longError, Snapshot("claude", 0, 0, ProviderStatus.Error) with { Message = new string('x', 9000) } });
        Check(popup.Height < 500, "Long provider errors cannot expand popup beyond Full HD work area");
        var expiredClaude = UsageSnapshot.ForState("claude", "Claude Code", ProviderStatus.Unavailable, "Claude login expired; open Claude Code once to refresh it.", "pro");
        popup.Render(new[] { Snapshot("codex", 40), expiredClaude });
        Application.DoEvents();
        Check(renew.Visible && renew.Bottom < popup.ClientSize.Height - 20, "Expired Claude login shows the renew button above the footer");
        Check(renew.Width <= popup.ClientSize.Width - 40 && renew.Left >= 20, "Renew button fits inside the popup");
        Check(renew.GetPreferredSize(Size.Empty).Width <= renew.Width, "Renew button text fits at current display DPI");
        renew.PerformClick();
        Check(renewals == 1 && refreshes == 2, "Renew button invokes renewal only");
        foreach (bool light in new[] { true, false })
        {
            popup.PreviewTheme(light);
            using var renewShot = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(renewShot, new Rectangle(Point.Empty, renewShot.Size));
            renewShot.Save(Path.Combine(output, light ? "popup-claude-expired-light.png" : "popup-claude-expired-dark.png"));
        }
        popup.Render(new[] { Snapshot("codex", 40), Snapshot("claude", 0, 0, ProviderStatus.Error) });
        Check(!renew.Visible, "Transient Claude errors do not show the renew button");
        using (var noRenew = new PopupForm(() => { }))
        {
            _ = noRenew.Handle;
            noRenew.Render(new[] { expiredClaude });
            Check(!noRenew.Controls.OfType<Button>().Any(b => b.Visible && b.AccessibleName != "Refresh usage"), "Previews without a renew action show no button");
        }
        popup.Render(new[] { Snapshot("codex", 100), Snapshot("claude", 100, 0) });
        using var limitsShot = new Bitmap(popup.Width, popup.Height);
        popup.DrawToBitmap(limitsShot, new Rectangle(Point.Empty, limitsShot.Size));
        limitsShot.Save(Path.Combine(output, "popup-zero-and-full.png"));
        Check(PopupForm.FormatFraction(double.NaN) == "—" && PopupForm.FormatFraction(double.PositiveInfinity) == "—", "Invalid usage is not misrepresented as zero");
        popup.Close();
        using var settingsForm = new SettingsForm(new AppSettings());
        settingsForm.StartPosition = FormStartPosition.Manual;
        settingsForm.Location = new Point(-4000, -4000);
        settingsForm.Show();
        Application.DoEvents();
        void CheckContentFits(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is CheckBox or Button or Label)
                {
                    var preferred = child.GetPreferredSize(Size.Empty);
                    Check(child.Width >= preferred.Width && child.Height >= preferred.Height, "Settings controls fit their DPI-scaled text");
                }
                CheckContentFits(child);
            }
        }
        CheckContentFits(settingsForm);
        Check(settingsForm.Width < 700 && settingsForm.Height < 700, "Settings fit Full HD at the current scale");
        using var settingsShot = new Bitmap(settingsForm.Width, settingsForm.Height);
        settingsForm.DrawToBitmap(settingsShot, new Rectangle(Point.Empty, settingsShot.Size));
        settingsShot.Save(Path.Combine(output, "settings.png"));
        settingsForm.Close();
    }
    catch (Exception ex) { popupFailure = ex; }
});
ui.SetApartmentState(ApartmentState.STA);
ui.Start(); ui.Join();
if (popupFailure != null) throw popupFailure;
Console.WriteLine($"PASS: {count} weekly-selection, tray, circular-popup, theme, and refresh checks.");
