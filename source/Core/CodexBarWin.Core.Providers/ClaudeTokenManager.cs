#nullable enable
using System;

namespace CodexBarWin.Core.Providers;

internal sealed class ClaudeTokenManager
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    // Read-only mode never rotates the CLI's refresh token, so it waits for Claude Code to refresh its own login.
    private const string ExpiredReadOnly = "Claude login expired; open Claude Code once to refresh it.";
    private readonly ClaudeCredentialStore _store;
    private readonly IClaudeOAuthRefresher _refresher;
    private readonly bool _allowRefresh;
    private ClaudeCredentials? _session;
    private bool _persistFailed;

    public ClaudeTokenManager(ClaudeCredentialStore store, IClaudeOAuthRefresher refresher, bool allowRefresh = true)
    { _store = store; _refresher = refresher; _allowRefresh = allowRefresh; }

    public ClaudeTokenResult GetFreshCredentials()
    {
        var cli = _store.ReadFromCli();
        var stored = _store.ReadFromStore();
        var selected = ClaudeCredentialStore.Freshest(ClaudeCredentialStore.Freshest(cli, stored), _session);
        if (selected == null) return new(null, "No reusable local login; run Claude to log in.");
        if (!selected.HasProfileScope) return new(null, "Login lacks user:profile scope; log in again with Claude.");
        if (!_allowRefresh)
        {
            if (selected.ExpiresAt.HasValue && selected.ExpiresAt.Value <= DateTimeOffset.UtcNow)
                return new(null, ExpiredReadOnly);
            return Remember(selected);
        }
        if (!NeedsRefresh(selected)) return Remember(selected);
        var refreshed = TryRefresh(selected);
        if (refreshed.Credentials != null) return refreshed;
        var alternative = ClaudeCredentialStore.SameCredentials(selected, stored) ? cli : stored;
        if (alternative != null && alternative.HasProfileScope && !ClaudeCredentialStore.SameCredentials(selected, alternative))
        {
            if (!NeedsRefresh(alternative)) return Remember(alternative);
            if (alternative.RefreshToken != selected.RefreshToken) return TryRefresh(alternative);
        }
        return refreshed;
    }

    public ClaudeTokenResult ForceRefresh()
    {
        // No error of our own: the caller keeps the usage endpoint's 401 message.
        if (!_allowRefresh) return new(null, null);
        var selected = ClaudeCredentialStore.Freshest(
            ClaudeCredentialStore.Freshest(_store.ReadFromCli(), _store.ReadFromStore()), _session);
        if (selected == null || !selected.HasProfileScope) return new(null, "No reusable scoped login; run Claude to log in.");
        return TryRefresh(selected);
    }

    private ClaudeTokenResult TryRefresh(ClaudeCredentials credentials)
    {
        if (string.IsNullOrEmpty(credentials.RefreshToken)) return new(null, "Login expired and no refresh token is available; log in with Claude.");
        try
        {
            var result = _refresher.Refresh(credentials.RefreshToken);
            return Remember(credentials with
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken ?? credentials.RefreshToken,
                ExpiresAt = result.ExpiresAt
            });
        }
        catch (ClaudeFetchException ex) { return new(null, ex.Message); }
    }

    private ClaudeTokenResult Remember(ClaudeCredentials credentials)
    {
        // A disk failure must not discard an otherwise usable refreshed token in this process.
        if (!ClaudeCredentialStore.SameCredentials(_session, credentials) || _persistFailed)
            _persistFailed = !_store.WriteToStore(credentials);
        _session = credentials;
        return new(credentials, _persistFailed ? "Protected login cache could not be saved; login is usable only in memory until this is resolved." : _store.Warning);
    }

    private static bool NeedsRefresh(ClaudeCredentials credentials) => credentials.ExpiresAt.HasValue &&
        credentials.ExpiresAt.Value - DateTimeOffset.UtcNow <= RefreshSkew;
}
