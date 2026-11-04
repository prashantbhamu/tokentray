using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodexBarWin.Core.Models;
using CodexBarWin.Core.Readers;

namespace CodexBarWin.Core.Providers;

public sealed class CodexProvider : IProvider
{
    private readonly record struct CodexAuth(string AccessToken, string? AccountId);

    private readonly string _codexDir;

    private readonly ICodexUsageClient? _usageClient;

    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30.0);

    public string Id => "codex";

    public string DisplayName => "Codex";

    private string LogsDbPath => Path.Combine(_codexDir, "logs_2.sqlite");

    private string AuthPath => Path.Combine(_codexDir, "auth.json");

    private string ConfigPath => Path.Combine(_codexDir, "config.toml");

    private string SessionsPath => Path.Combine(_codexDir, "sessions");

    public CodexProvider(ICodexUsageClient? usageClient = null, string? codexDir = null)
    {
        _usageClient = usageClient;
        _codexDir = codexDir ?? Path.Combine(UserProfile(), ".codex");
    }

    public bool IsInstalled()
    {
        if (Directory.Exists(_codexDir))
        {
            if (!File.Exists(AuthPath) && !File.Exists(LogsDbPath))
            {
                return Directory.Exists(SessionsPath);
            }
            return true;
        }
        return false;
    }

    public UsageSnapshot Refresh()
    {
        if (!IsInstalled())
        {
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.NotInstalled, _codexDir + " not found");
        }
        string? liveFailure = null;
        if (_usageClient == null)
        {
            Diag.Log("codex: usage client not configured (network off) → local log");
        }
        else
        {
            CodexAuth? codexAuth = TryReadAuth();
            if (codexAuth.HasValue)
            {
                CodexAuth valueOrDefault = codexAuth.GetValueOrDefault();
                CodexUsageApi codexUsageApi = _usageClient.Fetch(valueOrDefault.AccessToken, valueOrDefault.AccountId);
                if ((object)codexUsageApi != null)
                {
                    return BuildFromApi(codexUsageApi);
                }
                liveFailure = _usageClient.LastFailure;
                Diag.Log("codex: API returned null → falling back to local log");
            }
            else
            {
                Diag.Log("codex: auth.json token unreadable → local log");
            }
        }
        var local = RefreshLocal();
        // Say why live data is missing (e.g. an expired login) instead of silently showing older local data.
        return liveFailure == null ? local : local with { Message = local.Message == null ? liveFailure : liveFailure + " · " + local.Message };
    }

    private UsageSnapshot RefreshLocal()
    {
        CodexSessionUsage codexSessionUsage = new CodexSessionReader(SessionsPath).ReadLatestUsage();
        if ((object)codexSessionUsage != null)
        {
            return BuildFromSession(codexSessionUsage);
        }
        if (!File.Exists(LogsDbPath))
        {
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.NoData, "no usage found in session records; logs_2.sqlite not found", ReadPlanHint());
        }
        CodexLogEntry codexLogEntry = new CodexLogReader(LogsDbPath).ReadLatestRateLimit();
        CodexRateLimits codexRateLimits = CodexRateLimitParser.Parse(codexLogEntry?.Body);
        if ((object)codexRateLimits == null)
        {
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.NoData, "no rate-limit entry found in logs", ReadPlanHint());
        }
        List<Meter> meters = new List<Meter>();
        List<ResetWindow> resets = new List<ResetWindow>();
        DateTimeOffset? sourceUpdatedAt = LogTimestamp(codexLogEntry?.TimestampUnix);
        AddWindow(meters, resets, "Session", codexRateLimits.Primary, sourceUpdatedAt);
        AddWindow(meters, resets, "Weekly", codexRateLimits.Secondary, sourceUpdatedAt);
        string message = LocalNote(sourceUpdatedAt) + (codexRateLimits.LimitReached ? " · limit reached" : "");
        return new UsageSnapshot(Id, DisplayName, ReadPlanHint(), meters, resets, ProviderStatus.Ok, message, DateTimeOffset.UtcNow,
            UsageDataSource.LocalLog, sourceUpdatedAt);
    }

    private UsageSnapshot BuildFromSession(CodexSessionUsage usage)
    {
        List<Meter> meters = new List<Meter>();
        List<ResetWindow> resets = new List<ResetWindow>();
        AddWindow(meters, resets, "Session", usage.Primary, usage.SourceUpdatedAt);
        AddWindow(meters, resets, "Weekly", usage.Secondary, usage.SourceUpdatedAt);
        return new UsageSnapshot(Id, DisplayName, PlanLabel(usage.PlanType) ?? ReadPlanHint(), meters, resets, ProviderStatus.Ok,
            LocalNote(usage.SourceUpdatedAt), DateTimeOffset.UtcNow, UsageDataSource.LocalSession, usage.SourceUpdatedAt);
    }

    private UsageSnapshot BuildFromApi(CodexUsageApi api)
    {
        List<Meter> list = new List<Meter>();
        List<ResetWindow> resets = new List<ResetWindow>();
        AddApiWindow(list, resets, "Session", api.Primary);
        AddApiWindow(list, resets, "Weekly", api.Secondary);
        if (list.Count == 0)
        {
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.NoData, "no usage windows", PlanLabel(api.PlanType));
        }
        var now = DateTimeOffset.UtcNow;
        return new UsageSnapshot(Id, DisplayName, PlanLabel(api.PlanType) ?? ReadPlanHint(), list, resets, ProviderStatus.Ok, null, now, UsageDataSource.Live, now);
    }

    private static void AddApiWindow(List<Meter> meters, List<ResetWindow> resets, string fallback, CodexApiWindow? w)
    {
        if ((object)w != null)
        {
            string label = WindowLabel(w.WindowMinutes) ?? fallback;
            meters.Add(new Meter(label, w.UsedPercent, 100.0, "%"));
            if (w.ResetsAt is { } resetsAt) resets.Add(new ResetWindow(label, resetsAt));
        }
    }

    private static string? PlanLabel(string? planType)
    {
        if (!string.IsNullOrEmpty(planType))
        {
            return char.ToUpperInvariant(planType[0]) + planType.Substring(1, planType.Length - 1);
        }
        return null;
    }

    private CodexAuth? TryReadAuth()
    {
        try
        {
            if (!File.Exists(AuthPath))
            {
                return null;
            }
            using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(AuthPath));
            if (!jsonDocument.RootElement.TryGetProperty("tokens", out var value) || value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            JsonElement value2;
            string text = ((value.TryGetProperty("access_token", out value2) && value2.ValueKind == JsonValueKind.String) ? value2.GetString() : null);
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            JsonElement value3;
            string accountId = ((value.TryGetProperty("account_id", out value3) && value3.ValueKind == JsonValueKind.String) ? value3.GetString() : null);
            return new CodexAuth(text, accountId);
        }
        catch
        {
            return null;
        }
    }

    private static void AddWindow(List<Meter> meters, List<ResetWindow> resets, string label, CodexWindow? w, DateTimeOffset? recordedAt)
    {
        if ((object)w != null)
        {
            string label2 = WindowLabel(w.WindowMinutes) ?? label;
            meters.Add(new Meter(label2, w.UsedPercent, 100.0, "%"));
            if (w.ResetsAt(recordedAt) is { } resetsAt) resets.Add(new ResetWindow(label2, resetsAt));
        }
    }

    private static string LocalNote(DateTimeOffset? timestamp) => !timestamp.HasValue
        ? "Local data · time unknown (not live)"
        : $"Local data · as of {timestamp.Value.ToLocalTime():dd MMM HH:mm}" +
            (DateTimeOffset.UtcNow - timestamp.Value >= StaleAfter ? " · stale" : " (not live)");

    private static DateTimeOffset? LogTimestamp(long? timestamp)
    {
        try { return timestamp.HasValue && timestamp.Value > 0 ? DateTimeOffset.FromUnixTimeSeconds(timestamp.Value) : null; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static string? WindowLabel(int windowMinutes)
    {
        if (windowMinutes <= 360)
        {
            if (windowMinutes <= 60)
            {
                if (windowMinutes == 0)
                {
                    return null;
                }
                return "Hourly";
            }
            return "Session (5h)";
        }
        if (windowMinutes <= 1440)
        {
            return "Daily";
        }
        return "Weekly";
    }

    private string? ReadPlanHint()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return null;
            }
            foreach (string item in File.ReadLines(ConfigPath))
            {
                Match match = Regex.Match(item, "^\\s*model\\s*=\\s*\"([^\"]+)\"");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static string UserProfile()
    {
        return Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
