namespace CodexBarWin.Core.Providers;

public interface IClaudeUsageClient
{
    ClaudeUsageResponse Fetch(string accessToken);
}
