namespace CodexBarWin.Core.Providers;

public interface ICodexUsageClient
{
    CodexUsageApi? Fetch(string accessToken, string? accountId);
}
