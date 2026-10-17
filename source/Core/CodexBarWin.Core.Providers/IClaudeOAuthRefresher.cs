namespace CodexBarWin.Core.Providers;

public interface IClaudeOAuthRefresher
{
    ClaudeRefreshResult Refresh(string refreshToken);
}
