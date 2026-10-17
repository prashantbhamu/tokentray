using System;

namespace CodexBarWin.Core.Providers;

public sealed record CodexApiWindow(int UsedPercent, long ResetAt, int LimitWindowSeconds)
{
    public DateTimeOffset ResetsAt => DateTimeOffset.FromUnixTimeSeconds(ResetAt);

    public int WindowMinutes => LimitWindowSeconds / 60;
}
