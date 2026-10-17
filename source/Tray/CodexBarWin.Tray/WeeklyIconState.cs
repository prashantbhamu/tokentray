using System;
using System.Collections.Generic;
using System.Linq;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

internal enum WeeklyLeader { None, Codex, Claude }

internal sealed record WeeklyIconState(double? Codex, double? Claude)
{
    public WeeklyLeader Leader => Codex.HasValue && (!Claude.HasValue || Codex.Value >= Claude.Value)
        ? WeeklyLeader.Codex : Claude.HasValue ? WeeklyLeader.Claude : WeeklyLeader.None;
    public double? Highest => Leader == WeeklyLeader.Codex ? Codex : Leader == WeeklyLeader.Claude ? Claude : null;
    public string Number => Highest.HasValue
        ? Math.Round(Highest.Value * 100, MidpointRounding.AwayFromZero).ToString("0") : "–";

    public static WeeklyIconState FromSnapshots(IReadOnlyList<UsageSnapshot> snapshots) =>
        new(FindWeekly(snapshots, "codex"), FindWeekly(snapshots, "claude"));

    private static double? FindWeekly(IReadOnlyList<UsageSnapshot> snapshots, string provider)
    {
        var meter = snapshots.FirstOrDefault(s => s.ProviderId == provider && s.Status == ProviderStatus.Ok)
            ?.Meters.FirstOrDefault(m => m.Label.StartsWith("Weekly", StringComparison.OrdinalIgnoreCase));
        return meter != null && meter.Limit > 0 && double.IsFinite(meter.Fraction)
            ? Math.Clamp(meter.Fraction, 0, 1) : null;
    }

    public string Tooltip => $"Codex {Format(Codex)} · Claude {Format(Claude)}";
    private static string Format(double? value) => value.HasValue ? $"{value.Value:P0}" : "unavailable";
}
