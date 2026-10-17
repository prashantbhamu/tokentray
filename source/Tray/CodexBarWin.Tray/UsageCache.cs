using System.Collections.Generic;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

internal sealed class UsageCache
{
    private readonly Dictionary<string, UsageSnapshot> _lastGood = new();
    private readonly HashSet<string> _stale = new();
    public bool IsStale => _stale.Count > 0;

    public UsageSnapshot Apply(UsageSnapshot snapshot)
    {
        _stale.Remove(snapshot.ProviderId);
        if (snapshot.Status == ProviderStatus.Ok)
        {
            _lastGood[snapshot.ProviderId] = snapshot;
            return snapshot;
        }
        if (snapshot.Status is ProviderStatus.Unavailable or ProviderStatus.Error &&
            _lastGood.TryGetValue(snapshot.ProviderId, out var cached))
        {
            _stale.Add(snapshot.ProviderId);
            var asOf = cached.SourceUpdatedAt ?? (cached.Source is UsageDataSource.Live or UsageDataSource.Unknown ? cached.FetchedAt : null);
            string time = asOf.HasValue ? asOf.Value.ToLocalTime().ToString("dd MMM HH:mm") : "time unknown";
            return cached with { Message = $"{snapshot.Message ?? "Temporarily unavailable"} · showing {time}", Source = UsageDataSource.Cached, SourceUpdatedAt = asOf };
        }
        _lastGood.Remove(snapshot.ProviderId);
        return snapshot;
    }

    public void Clear() { _lastGood.Clear(); _stale.Clear(); }
}
