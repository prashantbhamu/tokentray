using System;
using System.Collections.Generic;

namespace CodexBarWin.Core.Models;

public sealed record UsageSnapshot(string ProviderId, string DisplayName, string? Plan, IReadOnlyList<Meter> Meters, IReadOnlyList<ResetWindow> Resets, ProviderStatus Status, string? Message, DateTimeOffset FetchedAt,
    UsageDataSource Source = UsageDataSource.Unknown, DateTimeOffset? SourceUpdatedAt = null)
{
    public static UsageSnapshot ForState(string id, string name, ProviderStatus status, string? message, string? plan = null)
    {
        return new UsageSnapshot(id, name, plan, Array.Empty<Meter>(), Array.Empty<ResetWindow>(), status, message, DateTimeOffset.UtcNow);
    }
}
