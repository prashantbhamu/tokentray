using System;

namespace CodexBarWin.Core.Providers;

public sealed record CodexWindow(double UsedPercent, int WindowMinutes, long ResetAfterSeconds, long ResetAt)
{
    public DateTimeOffset ResetsAt => DateTimeOffset.FromUnixTimeSeconds(ResetAt);
}
