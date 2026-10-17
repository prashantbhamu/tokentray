using System;
using System.Collections.Generic;
using CodexBarWin.Core;
using CodexBarWin.Core.Providers;

namespace CodexBarWin.Tray;

internal static class ProviderFactory
{
    internal static bool NetworkAllowed(bool setting, string? noNetwork) => setting &&
        noNetwork != "1" && !string.Equals(noNetwork, "true", StringComparison.OrdinalIgnoreCase);

    public static ProviderRegistry Create(AppSettings settings)
    {
        string? noNetwork = Environment.GetEnvironmentVariable("CODEXBARWIN_NO_NETWORK");
        var providers = new List<IProvider>();
        if (settings.CodexEnabled)
            providers.Add(new CodexProvider(NetworkAllowed(settings.CodexUseNetwork, noNetwork) ? new CodexUsageClient() : null));
        if (settings.ClaudeEnabled)
            providers.Add(new ClaudeProvider(NetworkAllowed(settings.ClaudeUseNetwork, noNetwork) ? new ClaudeUsageClient() : null));
        return new ProviderRegistry(providers);
    }
}
