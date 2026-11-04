using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodexBarWin.Core.Models;

/// <param name="AccountKey">Opaque, non-reversible id of the account the reading belongs to (null when unknown).
/// Kept in memory only, so a cached reading is never shown for a different account.</param>
public sealed record UsageSnapshot(string ProviderId, string DisplayName, string? Plan, IReadOnlyList<Meter> Meters, IReadOnlyList<ResetWindow> Resets, ProviderStatus Status, string? Message, DateTimeOffset FetchedAt,
    UsageDataSource Source = UsageDataSource.Unknown, DateTimeOffset? SourceUpdatedAt = null, [property: JsonIgnore] string? AccountKey = null)
{
    public static UsageSnapshot ForState(string id, string name, ProviderStatus status, string? message, string? plan = null)
    {
        return new UsageSnapshot(id, name, plan, Array.Empty<Meter>(), Array.Empty<ResetWindow>(), status, message, DateTimeOffset.UtcNow);
    }
}
