namespace CodexBarWin.Core.Providers;

/// <param name="Renewable">True when opening Claude Code can fix the missing login (signed out or expired);
/// false for problems it cannot fix, such as a login without the user:profile scope.</param>
public sealed record ClaudeTokenResult(ClaudeCredentials? Credentials, string? Error, bool Renewable = false);
