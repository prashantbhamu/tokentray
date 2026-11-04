using System;

namespace CodexBarWin.Core.Providers;

public sealed record CodexWindow(double UsedPercent, int WindowMinutes, long ResetAfterSeconds, long ResetAt)
{
    // Prefer the absolute reset time; otherwise derive it from the relative one and when it was recorded.
    public DateTimeOffset? ResetsAt(DateTimeOffset? recordedAt)
    {
        if (ResetAt > 0) return DateTimeOffset.FromUnixTimeSeconds(ResetAt);
        if (ResetAfterSeconds > 0 && recordedAt.HasValue) return recordedAt.Value.AddSeconds(ResetAfterSeconds);
        return null;
    }
}
