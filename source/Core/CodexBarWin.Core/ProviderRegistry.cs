using System;
using System.Collections.Generic;
using System.Linq;
using CodexBarWin.Core.Models;
using CodexBarWin.Core.Providers;

namespace CodexBarWin.Core;

public sealed class ProviderRegistry
{
    private readonly IReadOnlyList<IProvider> _providers;

    public IReadOnlyList<IProvider> Providers => _providers;

    public ProviderRegistry(IEnumerable<IProvider> providers)
    {
        _providers = providers.ToList();
    }

    public static ProviderRegistry CreateDefault()
    {
        string environmentVariable = Environment.GetEnvironmentVariable("CODEXBARWIN_NO_NETWORK");
        bool flag = ((environmentVariable == "1" || environmentVariable == "true") ? true : false);
        bool flag2 = flag;
        Diag.Log($"registry: NO_NETWORK={environmentVariable ?? "<null>"} noNetwork={flag2}");
        IClaudeUsageClient usageClient = (flag2 ? null : new ClaudeUsageClient());
        ICodexUsageClient usageClient2 = (flag2 ? null : new CodexUsageClient());
        return new ProviderRegistry(new IProvider[2]
        {
            new CodexProvider(usageClient2),
            new ClaudeProvider(usageClient)
        });
    }

    public IReadOnlyList<UsageSnapshot> RefreshAll()
    {
        List<UsageSnapshot> list = new List<UsageSnapshot>(_providers.Count);
        foreach (IProvider provider in _providers)
        {
            list.Add(SafeRefresh(provider));
        }
        return list;
    }

    private static UsageSnapshot SafeRefresh(IProvider p)
    {
        try
        {
            return p.Refresh();
        }
        catch (Exception ex)
        {
            return UsageSnapshot.ForState(p.Id, p.DisplayName, ProviderStatus.Error, ex.Message);
        }
    }
}
