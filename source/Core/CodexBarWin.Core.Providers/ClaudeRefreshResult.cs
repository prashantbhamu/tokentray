using System;

namespace CodexBarWin.Core.Providers;

public sealed record ClaudeRefreshResult(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt)
{
    public override string ToString() => "ClaudeRefreshResult { Tokens = [REDACTED] }";
}
