using System.Collections.Generic;
using System.Linq;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

internal static class UsagePresentation
{
    public static bool HasLocal(IReadOnlyList<UsageSnapshot> snapshots) => snapshots.Any(s => s.Source is UsageDataSource.LocalSession or UsageDataSource.LocalLog);
    public static bool HasCached(IReadOnlyList<UsageSnapshot> snapshots) => snapshots.Any(s => s.Source == UsageDataSource.Cached);
    public static string TooltipSuffix(IReadOnlyList<UsageSnapshot> snapshots) =>
        HasLocal(snapshots) && HasCached(snapshots) ? " · local/cached" : HasLocal(snapshots) ? " · local" : HasCached(snapshots) ? " · cached" : "";
    public static string Footer(IReadOnlyList<UsageSnapshot> snapshots, string clock) =>
        HasLocal(snapshots) || HasCached(snapshots) ? $"Checked {clock} · local/cached data" : $"Updated {clock} · usage shown";
}
