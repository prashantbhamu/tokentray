using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using CodexBarWin.Core.Models;
using CodexBarWin.Tray;

int count = 0;
void Check(bool condition, string description) { if (!condition) throw new Exception(description); count++; }
UsageSnapshot Snapshot(string id, double weekly, double session = 0, ProviderStatus status = ProviderStatus.Ok) =>
    new(id, id, null, new[] { new Meter("Session (5h)", session, 100, "%"), new Meter("Weekly", weekly, 100, "%") },
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

var output = args.Length > 0 ? args[0] : "test-results";
Directory.CreateDirectory(output);
foreach (var (name, state, color) in new[] {("codex", codex, TrayIconRenderer.CodexColor), ("claude", State(40,60), TrayIconRenderer.ClaudeColor), ("hundred", State(100,20), TrayIconRenderer.CodexColor)})
{
    foreach (int size in new[] {16,20,24,32,128})
    {
        using var bitmap = TrayIconRenderer.Render(state, size);
        Check(bitmap.Width == size && bitmap.Height == size, "Exact native render size");
        int transparentPixels=0;
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) if(bitmap.GetPixel(x,y).A==0) transparentPixels++;
        // Tiny resampled glyph/rail edges carry partial alpha; test truly empty
        // area rather than demanding transparent pixels at an antialiased rail corner.
        Check(transparentPixels > size*size/8, "Empty icon area has no background tile");
        Check(bitmap.GetPixel(size/2,size/2).A == 0, "Hollow center stays transparent");
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
}
Console.WriteLine($"PASS: {count} weekly-selection, color, native-size, and vertical-bar checks.");
