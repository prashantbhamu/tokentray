using System;
using System.Linq;

namespace CodexBarWin.Core.Providers;

public sealed record ClaudeCredentials(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt, string[] Scopes, string? SubscriptionType)
{
    public override string ToString() => "ClaudeCredentials { Tokens = [REDACTED] }";
    public bool HasProfileScope => Scopes.Contains("user:profile");
}
