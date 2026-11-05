using System;
using System.Collections.Generic;
using System.Linq;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

internal sealed record WeeklyIconState(double? Codex, double? Claude, bool ClaudeNeedsLogin = false)
{
    public static WeeklyIconState FromSnapshots(IReadOnlyList<UsageSnapshot> snapshots) =>
        new(FindWeekly(snapshots, "codex"), FindWeekly(snapshots, "claude"), snapshots.Any(ClaudeRenewal.Applies));

    private static double? FindWeekly(IReadOnlyList<UsageSnapshot> snapshots, string provider)
    {
        var meter = snapshots.FirstOrDefault(s => s.ProviderId == provider && s.Status == ProviderStatus.Ok)
            ?.Meters.FirstOrDefault(m => m.Label.StartsWith("Weekly", StringComparison.OrdinalIgnoreCase));
        return meter != null && meter.Limit > 0 && double.IsFinite(meter.Fraction)
            ? Math.Clamp(meter.Fraction, 0, 1) : null;
    }

    public string Tooltip => $"Codex {Format(Codex)} · Claude {(ClaudeNeedsLogin ? "login needed" : Format(Claude))}";
    private static string Format(double? value) => value.HasValue ? $"{value.Value:P0}" : "unavailable";
}
