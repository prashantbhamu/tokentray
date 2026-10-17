using System;

namespace CodexBarWin.Core.Models;

public sealed record ResetWindow(string Label, DateTimeOffset ResetsAt)
{
    public TimeSpan TimeUntilReset => ResetsAt - DateTimeOffset.UtcNow;
}
