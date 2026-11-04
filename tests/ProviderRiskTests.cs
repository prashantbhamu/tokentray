using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodexBarWin.Core.Models;
using CodexBarWin.Core.Providers;
using CodexBarWin.Core.Readers;
using CodexBarWin.Tray;
using Microsoft.Data.Sqlite;

internal static class ProviderRiskTests
{
    // Every credential written here is fake; no real account paths are used.
    public static void Run(string output, Action<bool, string> check)
    {
        string root = Path.Combine(output, "provider-fixtures-" + Guid.NewGuid().ToString("N"));
        string Area(string name) { string path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        var credentials = new ClaudeCredentials("fake-access-only", "fake-refresh-only",
            DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds()), new[] { "user:profile" }, "pro");
        string TokenJson(ClaudeCredentials c) => JsonSerializer.Serialize(new
        {
            accessToken = c.AccessToken,
            refreshToken = c.RefreshToken,
            expiresAt = c.ExpiresAt?.ToUnixTimeMilliseconds(),
            scopes = c.Scopes,
            subscriptionType = c.SubscriptionType
        });
        string Cli(string directory, ClaudeCredentials c)
        {
            Directory.CreateDirectory(directory);
            string json = "{\"claudeAiOauth\":" + TokenJson(c) + "}";
            File.WriteAllText(Path.Combine(directory, ".credentials.json"), json);
            return json;
        }

        string cacheDir = Area("roundtrip"), cliDir = Area("cli");
        var store = new ClaudeCredentialStore(cliDir, cacheDir);
        check(store.WriteToStore(credentials), "DPAPI token write succeeds");
        string secure = Path.Combine(cacheDir, "claude-token.dpapi");
        byte[] cipher = File.ReadAllBytes(secure);
        check(!Encoding.UTF8.GetString(cipher).Contains(credentials.AccessToken) && !Encoding.UTF8.GetString(cipher).Contains(credentials.RefreshToken!), "Persisted bytes contain no readable fake tokens");
        check(!File.Exists(Path.Combine(cacheDir, "claude-token.json")), "New cache never creates plaintext JSON");
        var reopened = new ClaudeCredentialStore(cliDir, cacheDir).ReadFromStore();
        check(reopened?.AccessToken == credentials.AccessToken && reopened.RefreshToken == credentials.RefreshToken && reopened.ExpiresAt == credentials.ExpiresAt, "New store instance decrypts the complete login");
        check(reopened!.HasProfileScope, "Encrypted round trip preserves profile scope");
        check(!ClaudeCredentialStore.SameCredentials(credentials, credentials with { Scopes = Array.Empty<string>() }), "Cache equality includes scopes so migration cannot lose profile access");
        check(!credentials.ToString().Contains(credentials.AccessToken) && !new ClaudeRefreshResult(credentials.AccessToken, credentials.RefreshToken, DateTimeOffset.UtcNow).ToString().Contains(credentials.AccessToken), "Credential record diagnostics redact tokens");

        string migration = Area("migration"), migrationCli = Area("migration-cli");
        string cliBefore = Cli(migrationCli, credentials);
        File.WriteAllText(Path.Combine(migration, "claude-token.json"), TokenJson(credentials));
        var migrated = new ClaudeCredentialStore(migrationCli, migration).ReadFromStore();
        check(migrated?.AccessToken == credentials.AccessToken && File.Exists(Path.Combine(migration, "claude-token.dpapi")), "Legacy login migrates to DPAPI without losing credentials");
        check(!File.Exists(Path.Combine(migration, "claude-token.json")), "Verified same-directory migration retires old plaintext cache");
        check(File.ReadAllText(Path.Combine(migrationCli, ".credentials.json")) == cliBefore, "Migration never changes the Claude CLI login file");

        string main = Area("side-by-side-main"), preview = Area("side-by-side-preview");
        string legacy = Path.Combine(main, "claude-token.json");
        string legacyJson = TokenJson(credentials);
        File.WriteAllText(legacy, legacyJson);
        var previewStore = new ClaudeCredentialStore(cliDir, preview, main, retireLegacy: false);
        check(previewStore.ReadFromStore()?.AccessToken == credentials.AccessToken && File.Exists(Path.Combine(preview, "claude-token.dpapi")), "Side-by-side preview imports only an encrypted copy");
        check(File.ReadAllText(legacy) == legacyJson, "Preview leaves installed main cache untouched");

        var failing = new ClaudeCredentialStore(cliDir, cacheDir, protector: new FailingProtector());
        check(!failing.WriteToStore(credentials with { AccessToken = "fake-other" }), "Encryption failure is reported");
        check(File.ReadAllBytes(secure).SequenceEqual(cipher), "Failed encryption preserves prior encrypted cache");
        check(Directory.GetFiles(cacheDir, "*.tmp").Length == 0 && !File.Exists(Path.Combine(cacheDir, "claude-token.json")), "Failed write leaves no temporary or plaintext fallback");
        string failedMigration = Area("failed-migration");
        File.WriteAllText(Path.Combine(failedMigration, "claude-token.json"), legacyJson);
        var failedStore = new ClaudeCredentialStore(cliDir, failedMigration, protector: new FailingProtector());
        check(failedStore.ReadFromStore() != null && File.Exists(Path.Combine(failedMigration, "claude-token.json")) && failedStore.Warning != null, "Failed migration retains recoverable legacy login and surfaces a warning");
        string badVerification = Area("bad-verification");
        check(!new ClaudeCredentialStore(cliDir, badVerification, protector: new BadVerificationProtector()).WriteToStore(credentials) && !File.Exists(Path.Combine(badVerification, "claude-token.dpapi")), "Round-trip verification prevents a bad replacement");

        string corruptDir = Area("corrupt");
        File.WriteAllBytes(Path.Combine(corruptDir, "claude-token.dpapi"), new byte[] { 1, 2, 3, 4 });
        var corrupt = new ClaudeCredentialStore(cliDir, corruptDir);
        check(corrupt.ReadFromStore() == null && corrupt.Warning != null, "Corrupt cache fails safely without leaking data");
        Cli(cliDir, credentials);
        check(new ClaudeTokenManager(corrupt, new FakeRefresher()).GetFreshCredentials().Credentials != null, "Valid CLI login recovers from a corrupt widget cache");

        string readOnlyLegacy = Area("read-only-legacy");
        string readOnlyPath = Path.Combine(readOnlyLegacy, "claude-token.json");
        File.WriteAllText(readOnlyPath, legacyJson);
        File.SetAttributes(readOnlyPath, FileAttributes.ReadOnly);
        var readOnlyStore = new ClaudeCredentialStore(cliDir, readOnlyLegacy);
        try
        {
            check(readOnlyStore.ReadFromStore() != null && readOnlyStore.Warning != null && File.Exists(readOnlyPath), "Failed plaintext retirement is reported");
            readOnlyStore.WriteToStore(credentials);
            check(readOnlyStore.Warning != null, "A subsequent protected write does not hide a retirement warning");
        }
        finally { File.SetAttributes(readOnlyPath, FileAttributes.Normal); }
        readOnlyStore.ReadFromStore();
        check(!File.Exists(readOnlyPath) && readOnlyStore.Warning == null, "Retirement retries safely when the file becomes writable");

        var expired = credentials with { ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) };
        string refreshCli = Area("refresh-cli"), refreshCache = Area("refresh-cache");
        string expiredCliBefore = Cli(refreshCli, expired);
        var refresh = new FakeRefresher(); var usage = new FakeClaudeUsage();
        var provider = new ClaudeProvider(usage, refreshCli, refreshCache, refresh);
        var live = provider.Refresh();
        check(live.Status == ProviderStatus.Ok && live.Source == UsageDataSource.Live && live.SourceUpdatedAt.HasValue, "Expired login refresh still yields live Claude usage");
        check(refresh.Calls == 1 && usage.LastToken == "fake-rotated-access", "Live fetch uses the refreshed access token");
        check(File.Exists(Path.Combine(refreshCache, "claude-token.dpapi")) && !File.Exists(Path.Combine(refreshCache, "claude-token.json")), "Refreshed login is persisted only as DPAPI");
        check(File.ReadAllText(Path.Combine(refreshCli, ".credentials.json")) == expiredCliBefore, "Token refresh does not overwrite CLI-owned credentials");
        var restartedRefresh = new FakeRefresher();
        check(new ClaudeProvider(new FakeClaudeUsage(), refreshCli, refreshCache, restartedRefresh).Refresh().Status == ProviderStatus.Ok && restartedRefresh.Calls == 0, "Restart reuses protected refreshed login without refreshing again");

        string retryCli = Area("retry-cli"), retryCache = Area("retry-cache"); Cli(retryCli, credentials);
        var retryRefresh = new FakeRefresher(); var retryUsage = new FakeClaudeUsage { UnauthorizedFirst = true };
        check(new ClaudeProvider(retryUsage, retryCli, retryCache, retryRefresh).Refresh().Status == ProviderStatus.Ok && retryUsage.Calls == 2 && retryRefresh.Calls == 1, "401 recovery refreshes once and retries live usage");
        string memoryCli = Area("memory-cli"); Cli(memoryCli, expired);
        var memoryRefresh = new FakeRefresher();
        var memoryManager = new ClaudeTokenManager(new ClaudeCredentialStore(memoryCli, Area("memory-cache"), protector: new FailingProtector()), memoryRefresh);
        var memoryFirst = memoryManager.GetFreshCredentials(); var memorySecond = memoryManager.GetFreshCredentials();
        check(memoryFirst.Credentials != null && memoryFirst.Error != null && memorySecond.Credentials != null && memoryRefresh.Calls == 1, "Cache write failure preserves usable refreshed login in memory with warning");

        var noRefresh = new FakeRefresher();
        var previewManager = new ClaudeTokenManager(new ClaudeCredentialStore(cliDir, Area("preview-policy")), noRefresh, allowRefresh: false);
        check(previewManager.GetFreshCredentials().Credentials != null && previewManager.ForceRefresh().Credentials == null && noRefresh.Calls == 0, "Preview can use login but cannot rotate the main app's tokens");
        string expiredPreviewCli = Area("expired-preview-cli"); Cli(expiredPreviewCli, expired);
        check(new ClaudeTokenManager(new ClaudeCredentialStore(expiredPreviewCli, Area("expired-preview")), noRefresh, allowRefresh: false).GetFreshCredentials().Credentials == null && noRefresh.Calls == 0, "Expired preview login waits rather than forcing OAuth refresh");
        var noScope = credentials with { Scopes = Array.Empty<string>(), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };
        string scopeCli = Area("scope-cli"), scopeCache = Area("scope-cache"); Cli(scopeCli, noScope);
        new ClaudeCredentialStore(scopeCli, scopeCache).WriteToStore(credentials);
        check(new ClaudeProvider(new FakeClaudeUsage(), scopeCli, scopeCache, new FakeRefresher()).Refresh().Status == ProviderStatus.Ok, "A valid scoped stored login wins over an unscoped CLI token");

        // Source timestamps come from usage records, never file modification or fetch time.
        var old = DateTimeOffset.UtcNow.AddDays(-1);
        string codexRoot = Area("codex-session"); string sessions = Path.Combine(codexRoot, "sessions"); Directory.CreateDirectory(sessions);
        string SessionJson(DateTimeOffset? timestamp) => JsonSerializer.Serialize(new
        {
            timestamp = timestamp?.ToString("O"),
            type = "event_msg",
            payload = new
            {
                type = "token_count",
                rate_limits = new
                {
                    plan_type = "pro",
                    secondary = new { used_percent = 35, window_minutes = 10080, resets_at = DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds() }
                }
            }
        });
        string sessionFile = Path.Combine(sessions, "rollout-fixture.jsonl");
        File.WriteAllText(sessionFile, SessionJson(old)); File.SetLastWriteTimeUtc(sessionFile, DateTime.UtcNow);
        var local = new CodexProvider(null, codexRoot).Refresh();
        check(local.Status == ProviderStatus.Ok && local.Source == UsageDataSource.LocalSession && local.SourceUpdatedAt == old, "Session fallback preserves original event time despite a freshly modified file");
        check(local.Message!.Contains("Local data") && local.Message.Contains("stale") && local.FetchedAt > old.AddHours(1), "Old local percentage cannot masquerade as a fresh reading");
        check(UsagePresentation.Footer(new[] { local }, "12:00").StartsWith("Checked") && UsagePresentation.TooltipSuffix(new[] { local }).Contains("local"), "Local provenance is visible in popup footer and tooltip");
        File.WriteAllText(sessionFile, SessionJson(null));
        var unknown = new CodexProvider(null, codexRoot).Refresh();
        check(unknown.SourceUpdatedAt == null && unknown.Message!.Contains("time unknown"), "Missing event time stays unknown, not file-modified time");
        var cached = new UsageCache(); cached.Apply(unknown);
        var unknownCached = cached.Apply(UsageSnapshot.ForState("codex", "Codex", ProviderStatus.Error, "offline"));
        check(unknownCached.Source == UsageDataSource.Cached && unknownCached.SourceUpdatedAt == null && unknownCached.Message!.Contains("time unknown"), "Caching local data does not fabricate its age");
        File.WriteAllText(sessionFile, SessionJson(DateTimeOffset.UtcNow).Replace("\"type\":\"token_count\"", "\"type\": \"token_count\""));
        var recent = new CodexProvider(null, codexRoot).Refresh();
        check(recent.Source == UsageDataSource.LocalSession && recent.Message!.Contains("not live"), "Recent local data is still identified as not live; JSON whitespace is accepted");

        string logsRoot = Area("codex-log");
        using (var database = new SqliteConnection($"Data Source={Path.Combine(logsRoot, "logs_2.sqlite")}"))
        {
            database.Open();
            using var command = database.CreateCommand();
            command.CommandText = "CREATE TABLE logs(id INTEGER PRIMARY KEY, feedback_log_body TEXT, ts INTEGER);"; command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO logs(feedback_log_body,ts) VALUES($body,$time)";
            command.Parameters.AddWithValue("$body", JsonSerializer.Serialize(new { rate_limits = new { secondary = new { used_percent = 25, window_minutes = 10080, reset_at = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds() } } }));
            command.Parameters.AddWithValue("$time", old.ToUnixTimeSeconds()); command.ExecuteNonQuery();
        }
        var log = new CodexProvider(null, logsRoot).Refresh();
        check(log.Source == UsageDataSource.LocalLog && log.SourceUpdatedAt?.ToUnixTimeSeconds() == old.ToUnixTimeSeconds() && log.Message!.Contains("Local data"), "SQLite fallback carries its original log timestamp and explicit local label");
        string apiRoot = Area("codex-api");
        File.WriteAllText(Path.Combine(apiRoot, "auth.json"), "{\"tokens\":{\"access_token\":\"fake-codex\",\"account_id\":\"fake-account\"}}");
        var apiClient = new FakeCodexUsage();
        var api = new CodexProvider(apiClient, apiRoot).Refresh();
        check(api.Source == UsageDataSource.Live && api.SourceUpdatedAt.HasValue && api.Message == null && apiClient.Calls == 1, "Successful Codex API result is genuinely marked live");
        check(UsagePresentation.TooltipSuffix(new[] { api }) == "" && UsagePresentation.Footer(new[] { api }, "12:00").StartsWith("Updated"), "Genuine live UI remains unchanged");
    }

    private sealed class FailingProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => throw new CryptographicException("Synthetic failure.");
        public byte[] Unprotect(byte[] ciphertext) => throw new CryptographicException("Synthetic failure.");
    }
    private sealed class BadVerificationProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => new byte[] { 1, 2, 3 };
        public byte[] Unprotect(byte[] ciphertext) => new byte[] { 9 };
    }
    private sealed class FakeRefresher : IClaudeOAuthRefresher
    {
        public int Calls;
        public ClaudeRefreshResult Refresh(string refreshToken) { Calls++; return new("fake-rotated-access", "fake-rotated-refresh", DateTimeOffset.UtcNow.AddHours(2)); }
    }
    private sealed class FakeClaudeUsage : IClaudeUsageClient
    {
        public bool UnauthorizedFirst;
        public int Calls;
        public string? LastToken;
        public ClaudeUsageResponse Fetch(string accessToken)
        {
            Calls++; LastToken = accessToken;
            if (UnauthorizedFirst && Calls == 1) throw new ClaudeFetchException(ClaudeFetchErrorKind.Unauthorized, "Synthetic unauthorized response.");
            return new(new(20, DateTimeOffset.UtcNow.AddHours(2).ToString("O")), new(19, DateTimeOffset.UtcNow.AddDays(5).ToString("O")), null, null);
        }
    }
    private sealed class FakeCodexUsage : ICodexUsageClient
    {
        public int Calls;
        public CodexUsageApi? Fetch(string accessToken, string? accountId) { Calls++; return new("pro", null, new(42, DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds(), 604800)); }
    }
    private sealed class FailingCodexUsage : ICodexUsageClient
    {
        public string? LastFailure => "Synthetic expired login";
        public CodexUsageApi? Fetch(string accessToken, string? accountId) => null;
    }

    // Regression checks for the pre-public review fixes.
    public static void RunReviewFixes(string output, Action<bool, string> check)
    {
        string root = Path.Combine(output, "review-fixtures-" + Guid.NewGuid().ToString("N"));
        string Area(string name) { string path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        string Cli(string directory, ClaudeCredentials c)
        {
            string json = "{\"claudeAiOauth\":" + JsonSerializer.Serialize(new
            {
                accessToken = c.AccessToken,
                refreshToken = c.RefreshToken,
                expiresAt = c.ExpiresAt?.ToUnixTimeMilliseconds(),
                scopes = c.Scopes,
                subscriptionType = c.SubscriptionType
            }) + "}";
            File.WriteAllText(Path.Combine(directory, ".credentials.json"), json);
            return json;
        }
        var valid = new ClaudeCredentials("fake-access-only", "fake-refresh-only", DateTimeOffset.UtcNow.AddHours(2), new[] { "user:profile" }, "pro");

        // Read-only Claude mode never rotates tokens, so Claude Code's own refresh token stays valid.
        string readOnlyCli = Area("readonly-cli"); string before = Cli(readOnlyCli, valid with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) });
        var readOnlyRefresh = new FakeRefresher();
        var expired = new ClaudeProvider(new FakeClaudeUsage(), readOnlyCli, Area("readonly-cache"), readOnlyRefresh, allowTokenRefresh: false).Refresh();
        check(expired.Status == ProviderStatus.Unavailable && readOnlyRefresh.Calls == 0 && expired.Message!.Contains("open Claude Code"), "Read-only mode waits for Claude Code to refresh an expired login");
        check(File.ReadAllText(Path.Combine(readOnlyCli, ".credentials.json")) == before, "Read-only mode leaves CLI credentials untouched");
        string unauthorizedCli = Area("readonly-401-cli"); Cli(unauthorizedCli, valid);
        var unauthorizedRefresh = new FakeRefresher(); var unauthorizedUsage = new FakeClaudeUsage { UnauthorizedFirst = true };
        var unauthorized = new ClaudeProvider(unauthorizedUsage, unauthorizedCli, Area("readonly-401-cache"), unauthorizedRefresh, allowTokenRefresh: false).Refresh();
        check(unauthorized.Status == ProviderStatus.Unavailable && unauthorizedRefresh.Calls == 0 && unauthorizedUsage.Calls == 1 && unauthorized.Message == "Synthetic unauthorized response.",
            "Read-only 401 reports the usage endpoint's reason instead of rotating tokens");

        // Codex percentages and reset times.
        var fractional = CodexUsageParser.Parse("{\"rate_limit\":{\"primary_window\":{\"used_percent\":23.5,\"reset_at\":0,\"limit_window_seconds\":18000}}}");
        check(fractional?.Primary?.UsedPercent == 23.5, "Fractional Codex API percentage is not truncated to zero");
        check(fractional?.Primary?.ResetsAt == null, "Missing Codex reset_at stays unknown instead of 1970");
        var relative = new CodexWindow(10, 300, 3600, 0);
        var recorded = DateTimeOffset.UtcNow.AddMinutes(-10);
        check(relative.ResetsAt(recorded) == recorded.AddHours(1) && relative.ResetsAt(null) == null, "Relative reset uses the record time, never the epoch");
        string apiRoot = Area("codex-api-noreset");
        File.WriteAllText(Path.Combine(apiRoot, "auth.json"), "{\"tokens\":{\"access_token\":\"fake-codex\"}}");
        var noReset = new CodexProvider(new FixedCodexUsage(fractional!), apiRoot).Refresh();
        check(noReset.Meters.Single().Used == 23.5 && noReset.Resets.Count == 0, "Codex window without a reset time shows no reset countdown");

        // An expired Codex login is surfaced while falling back to local data.
        string failRoot = Area("codex-expired"); string sessions = Path.Combine(failRoot, "sessions"); Directory.CreateDirectory(sessions);
        File.WriteAllText(Path.Combine(failRoot, "auth.json"), "{\"tokens\":{\"access_token\":\"fake-codex\"}}");
        string Event(int percent) => JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            type = "event_msg",
            payload = new { type = "token_count", rate_limits = new { secondary = new { used_percent = percent, window_minutes = 10080, resets_at = DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds() } } }
        });
        File.WriteAllText(Path.Combine(sessions, "rollout-expired.jsonl"), Event(12) + "\n");
        var fallback = new CodexProvider(new FailingCodexUsage(), failRoot).Refresh();
        check(fallback.Source == UsageDataSource.LocalSession && fallback.Message!.StartsWith("Synthetic expired login") && fallback.Message.Contains("Local data"),
            "Expired Codex login is reported alongside the local fallback");

        // Session files are read from the end; the newest event wins across chunk boundaries and CRLF.
        string big = Path.Combine(sessions, "rollout-big.jsonl");
        string filler = JsonSerializer.Serialize(new { type = "response_item", payload = new string('x', 5000) });
        var lines = Enumerable.Range(0, 40).Select(_ => filler).Prepend(Event(30)).Append(Event(55)).Concat(Enumerable.Range(0, 40).Select(_ => filler));
        File.WriteAllText(big, string.Join("\r\n", lines) + "\r\n");
        File.SetLastWriteTimeUtc(big, DateTime.UtcNow.AddMinutes(1));
        var newest = new CodexSessionReaderProbe(sessions).Read();
        check(newest?.Secondary?.UsedPercent == 55, "Backward session read finds the newest usage event past 64 KB of later output");
        check(CodexSessionReaderProbe.Lines(big).Count() == 82 && CodexSessionReaderProbe.Lines(big).All(l => !l.EndsWith('\r')), "Backward line reader returns every line once without CR");
    }

    private sealed class FixedCodexUsage(CodexUsageApi api) : ICodexUsageClient
    {
        public CodexUsageApi? Fetch(string accessToken, string? accountId) => api;
    }

    private sealed class CodexSessionReaderProbe(string sessions)
    {
        public CodexSessionUsage? Read() => new CodexSessionReader(sessions).ReadLatestUsage();
        public static System.Collections.Generic.IEnumerable<string> Lines(string path) => CodexSessionReader.LinesFromEnd(path);
    }
}
