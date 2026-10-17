using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CodexBarWin.Core.Models;
using CodexBarWin.Tray;

int count = 0;
void Check(bool condition, string description) { if (!condition) throw new Exception(description); count++; }
UsageSnapshot Snapshot(string id, double weekly, double session = 0, ProviderStatus status = ProviderStatus.Ok) =>
    new(id, id, null, id == "codex" ? new[] { new Meter("Weekly",weekly,100,"%") }
        : new[] { new Meter("Session (5h)", session, 100, "%"), new Meter("Weekly", weekly, 100, "%") },
        Array.Empty<ResetWindow>(), status, null, DateTimeOffset.UtcNow);
WeeklyIconState State(double c, double a, double session = 0) => WeeklyIconState.FromSnapshots(new[] { Snapshot("codex", c), Snapshot("claude", a, session) });

var codex = State(40, 19, 95);
Check(codex.Leader == WeeklyLeader.Codex && codex.Number == "40", "Session usage must not override weekly");
Check(State(40, 60).Leader == WeeklyLeader.Claude && State(40, 60).Number == "60", "Claude higher weekly selects orange");
Check(State(40, 40).Leader == WeeklyLeader.Codex, "Ties deterministically prefer Codex");
Check(State(0, 0).Number == "0", "Zero is valid usage");
Check(State(150, -5).Number == "100" && State(150, -5).Claude == 0, "Clamp provider out-of-range percentages");
Check(State(double.NaN, 19).Leader == WeeklyLeader.Claude, "Ignore invalid percentages");
var missing = WeeklyIconState.FromSnapshots(Array.Empty<UsageSnapshot>());
Check(missing.Leader == WeeklyLeader.None && missing.Number == "–", "No data must not fabricate zero");
Check(WeeklyIconState.FromSnapshots(new[] { Snapshot("codex", 80, 95, ProviderStatus.Error), Snapshot("claude", 19) }).Number == "19", "Unavailable provider must not win");
var sessionOnly = new UsageSnapshot("claude", "Claude", null, new[] {new Meter("Session (5h)", 99, 100, "%")}, Array.Empty<ResetWindow>(), ProviderStatus.Ok, null, DateTimeOffset.UtcNow);
Check(WeeklyIconState.FromSnapshots(new[] {sessionOnly}).Leader == WeeklyLeader.None, "No fallback from weekly to session");
Check(State(40, 19).Tooltip.Contains("Codex 40%") && State(40, 19).Tooltip.Contains("Claude 19%"), "Tooltip exposes both weekly values");
Check(!State(40,19).Tooltip.Contains("Weekly", StringComparison.OrdinalIgnoreCase), "Tooltip omits Weekly prefix");

var output = args.Length > 0 ? args[0] : "test-results";
Directory.CreateDirectory(output);
foreach (var (name, state, color) in new[] {("codex", codex, TrayIconRenderer.CodexColor), ("claude", State(40,60), TrayIconRenderer.ClaudeColor), ("hundred", State(100,20), TrayIconRenderer.CodexColor)})
{
    foreach (int size in new[] {16,20,24,32,128})
    {
        using var bitmap = TrayIconRenderer.Render(state, size);
        Check(bitmap.Width == size && bitmap.Height == size, "Exact native render size");
        int transparentPixels=0;
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) if(bitmap.GetPixel(x,y).A<16) transparentPixels++;
        // Tiny resampled glyph/rail edges carry partial alpha; test truly empty
        // area rather than demanding transparent pixels at an antialiased rail corner.
        Check(transparentPixels > size*size/16, $"Empty icon area has no background tile at {size}px ({transparentPixels} clear pixels)");
        Check(bitmap.GetPixel(size/2,size/2).A < 32, "Gap stays clear apart from tiny-size antialias fringe");
        Check(bitmap.GetPixel(0,0).A == 0, "No backing tile at outside corner");
        if(size == 128)
        {
            Check(bitmap.GetPixel(64,64).A == 0, "No numeral in hollow transparent center");
        }
        bitmap.Save(Path.Combine(output,$"{name}-{size}.png"),ImageFormat.Png);
    }
}
using (var meters = TrayIconRenderer.Render(State(40,19), 128))
{
    bool Close(Color a, Color b) => Math.Abs(a.R-b.R)<8 && Math.Abs(a.G-b.G)<8 && Math.Abs(a.B-b.B)<8;
    Check(Close(meters.GetPixel(34,110), TrayIconRenderer.CodexColor), "Left bar fills upward in Codex black");
    Check(Close(meters.GetPixel(94,110), TrayIconRenderer.ClaudeColor), "Right bar fills upward in Claude orange");
    Check(!Close(meters.GetPixel(34,30), TrayIconRenderer.CodexColor), "Unused left bar remains gray");
    Check(!Close(meters.GetPixel(94,30), TrayIconRenderer.ClaudeColor), "Unused right bar remains gray");
    Check(Close(meters.GetPixel(14,110), TrayIconRenderer.CodexColor), "Thicker left bar extends toward edge");
    Check(Close(meters.GetPixel(114,110), TrayIconRenderer.ClaudeColor), "Thicker right bar extends toward edge");
    Check(meters.GetPixel(64,110).A == 0, "Narrow gap still separates the bars");
    Check(Close(meters.GetPixel(10,40), TrayIconRenderer.CodexColor), "Black border encloses gray Codex track");
    Check(Close(meters.GetPixel(74,40), TrayIconRenderer.ClaudeColor), "Orange border encloses gray Claude track");
    Check(Close(meters.GetPixel(34,6), TrayIconRenderer.CodexColor), "Codex border includes top edge");
    Check(Close(meters.GetPixel(94,6), TrayIconRenderer.ClaudeColor), "Claude border includes top edge");
}
using (var zero = TrayIconRenderer.Render(State(0,0),128))
{
    Check(zero.GetPixel(10,40).R < 30, "Zero-usage Codex keeps its outline");
    Check(zero.GetPixel(74,40).R > 220 && zero.GetPixel(74,40).G < 140, "Zero-usage Claude keeps its outline");
    Check(zero.GetPixel(34,40).R > 200 && zero.GetPixel(94,40).G > 200, "Zero-usage interiors remain gray");
}
Exception? popupFailure = null;
var ui = new Thread(() => {
    try
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        int refreshes = 0;
        using var popup = new PopupForm(() => refreshes++);
        popup.Location = new Point(-4000,-4000);
        _ = popup.Handle;
        Check(!popup.ShowInTaskbar && popup.Opacity == 1, "Popup stays opaque and off taskbar");
        popup.Render(new[] { Snapshot("codex",40), Snapshot("claude",19,20) });
        Check(popup.Width == 360 && popup.Height < 340, $"Circular popup remains compact ({popup.Width}x{popup.Height})");
        popup.Show();
        Application.DoEvents();
        var refresh = popup.Controls.OfType<Button>().Single();
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
            using var shot = new Bitmap(popup.Width,popup.Height);
            popup.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));
            shot.Save(Path.Combine(output,light ? "popup-light.png" : "popup-dark.png"));
            bool HasColor(Color c) { int n=0; for(int y=15;y<shot.Height-30;y++) for(int x=18;x<82;x++) { var p=shot.GetPixel(x,y); if(Math.Abs(p.R-c.R)<5 && Math.Abs(p.G-c.G)<5 && Math.Abs(p.B-c.B)<5) n++; } return n>30; }
            Check(HasColor(TrayIconRenderer.CodexColor), "Popup ring uses Codex black");
            Check(HasColor(TrayIconRenderer.ClaudeColor), "Popup rings use Claude orange");
        }
        popup.Render(Array.Empty<UsageSnapshot>(),true);
        Check(popup.Height == 84 && !refresh.Enabled,"Loading state is compact and noninteractive");
        popup.Render(new[] { Snapshot("codex",40,0,ProviderStatus.Error), Snapshot("claude",19) });
        Check(popup.Height > 100 && popup.Height < 340,"Provider error retains a usable popup");
        using var errorShot = new Bitmap(popup.Width,popup.Height);
        popup.DrawToBitmap(errorShot,new Rectangle(Point.Empty,errorShot.Size));
        errorShot.Save(Path.Combine(output,"popup-error.png"));
        popup.Close();
    }
    catch(Exception ex) { popupFailure = ex; }
});
ui.SetApartmentState(ApartmentState.STA);
ui.Start(); ui.Join();
if (popupFailure != null) throw popupFailure;
Console.WriteLine($"PASS: {count} weekly-selection, tray, circular-popup, theme, and refresh checks.");
