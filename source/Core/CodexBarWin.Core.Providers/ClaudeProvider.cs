using System;
using System.Collections.Generic;
using System.IO;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Core.Providers;

public sealed class ClaudeProvider : IProvider
{
    private readonly string _claudeDir;

    private readonly IClaudeUsageClient? _usageClient;

    private readonly ClaudeCredentialStore _store;

    private readonly ClaudeTokenManager? _tokenManager;

    public string Id => "claude";

    public string DisplayName => "Claude Code";

    public ClaudeProvider(IClaudeUsageClient? usageClient = null, string? claudeDir = null, string? tokenStoreDir = null, IClaudeOAuthRefresher? refresher = null,
        bool allowTokenRefresh = true, string? legacyTokenStoreDir = null)
    {
        _usageClient = usageClient;
        _claudeDir = claudeDir ?? Path.Combine(UserProfile(), ".claude");
        string storeDir = tokenStoreDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexBarWin");
        _store = new ClaudeCredentialStore(_claudeDir, storeDir, legacyTokenStoreDir, retireLegacy: allowTokenRefresh);
        if (usageClient != null)
        {
            _tokenManager = new ClaudeTokenManager(_store, refresher ?? new ClaudeOAuthRefresher(), allowTokenRefresh);
        }
    }

    public bool IsInstalled()
    {
        return Directory.Exists(_claudeDir);
    }

    public UsageSnapshot Refresh()
    {
        if (!IsInstalled())
        {
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.NotInstalled, _claudeDir + " not found");
        }
        if (_usageClient == null || _tokenManager == null)
        {
            ClaudeCredentials claudeCredentials = _store.ReadFromCli();
            if ((object)claudeCredentials != null)
            {
                return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.NoData, "network disabled; seat-plan usage isn't stored locally", claudeCredentials.SubscriptionType);
            }
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.Unavailable, "no reusable local login (host-managed auth?); usage isn't stored locally");
        }
        ClaudeTokenResult freshCredentials = _tokenManager.GetFreshCredentials();
        if ((object)freshCredentials.Credentials == null)
        {
            Diag.Log("claude: no usable creds → " + freshCredentials.Error);
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.Unavailable, freshCredentials.Error, _store.ReadFromCli()?.SubscriptionType);
        }
        string subscriptionType = freshCredentials.Credentials.SubscriptionType;
        try
        {
            return FetchAndBuild(freshCredentials.Credentials.AccessToken, subscriptionType, freshCredentials.Error);
        }
        catch (ClaudeFetchException ex) when (ex.Kind == ClaudeFetchErrorKind.Unauthorized)
        {
            Diag.Log("claude: usage 401 → forcing token refresh + retry");
            ClaudeTokenResult claudeTokenResult = _tokenManager.ForceRefresh();
            if ((object)claudeTokenResult.Credentials == null)
            {
                return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.Unavailable, claudeTokenResult.Error ?? ex.Message, subscriptionType);
            }
            try
            {
                return FetchAndBuild(claudeTokenResult.Credentials.AccessToken, claudeTokenResult.Credentials.SubscriptionType ?? subscriptionType, claudeTokenResult.Error);
            }
            catch (ClaudeFetchException ex2)
            {
                Diag.Log($"claude: retry fetch {ex2.Kind}: {ex2.Message}");
                ProviderStatus status = ((ex2.Kind == ClaudeFetchErrorKind.Unauthorized) ? ProviderStatus.Unavailable : ProviderStatus.Error);
                return UsageSnapshot.ForState(Id, DisplayName, status, ex2.Message, subscriptionType);
            }
        }
        catch (ClaudeFetchException ex3)
        {
            Diag.Log($"claude: usage fetch {ex3.Kind}: {ex3.Message}");
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.Error, ex3.Message, subscriptionType);
        }
    }

    private UsageSnapshot FetchAndBuild(string accessToken, string? plan, string? warning)
    {
        ClaudeUsageResponse claudeUsageResponse = _usageClient.Fetch(accessToken);
        Diag.Log($"claude: usage 200 ok five_hour={claudeUsageResponse.FiveHour?.Utilization}");
        return BuildSnapshot(claudeUsageResponse, plan) with { Message = warning };
    }

    private UsageSnapshot BuildSnapshot(ClaudeUsageResponse usage, string? plan)
    {
        List<Meter> list = new List<Meter>();
        List<ResetWindow> resets = new List<ResetWindow>();
        Add(list, resets, "Session (5h)", usage.FiveHour);
        Add(list, resets, "Weekly", usage.SevenDay);
        if (list.Count == 0)
        {
            return UsageSnapshot.ForState(Id, DisplayName, ProviderStatus.NoData, "no active usage windows", plan);
        }
        var now = DateTimeOffset.UtcNow;
        return new UsageSnapshot(Id, DisplayName, plan, list, resets, ProviderStatus.Ok, null, now, UsageDataSource.Live, now);
    }

    private static void Add(List<Meter> meters, List<ResetWindow> resets, string label, ClaudeUsageWindow? w)
    {
        double? num = w?.Utilization;
        if (num.HasValue)
        {
            double valueOrDefault = num.GetValueOrDefault();
            meters.Add(new Meter(label, valueOrDefault, 100.0, "%"));
            DateTimeOffset? resetsAtOffset = w.ResetsAtOffset;
            if (resetsAtOffset.HasValue)
            {
                DateTimeOffset valueOrDefault2 = resetsAtOffset.GetValueOrDefault();
                resets.Add(new ResetWindow(label, valueOrDefault2));
            }
        }
    }

    private static string UserProfile()
    {
        return Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
