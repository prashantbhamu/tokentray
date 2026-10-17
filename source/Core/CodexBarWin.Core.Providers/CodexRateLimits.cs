namespace CodexBarWin.Core.Providers;

public sealed record CodexRateLimits(bool Allowed, bool LimitReached, CodexWindow? Primary, CodexWindow? Secondary);
