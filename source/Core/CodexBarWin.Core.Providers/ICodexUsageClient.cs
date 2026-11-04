namespace CodexBarWin.Core.Providers;

public interface ICodexUsageClient
{
    CodexUsageApi? Fetch(string accessToken, string? accountId);

    /// <summary>A user-facing reason the last Fetch returned null, when the user can act on it.</summary>
    string? LastFailure => null;
}
