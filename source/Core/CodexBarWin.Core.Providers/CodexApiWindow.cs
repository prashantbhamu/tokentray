using System;

namespace CodexBarWin.Core.Providers;

public sealed record CodexApiWindow(double UsedPercent, long ResetAt, int LimitWindowSeconds)
{
    // A missing reset_at must not become 1970 ("resets in now").
    public DateTimeOffset? ResetsAt => ResetAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(ResetAt) : null;

    public int WindowMinutes => LimitWindowSeconds / 60;
}
