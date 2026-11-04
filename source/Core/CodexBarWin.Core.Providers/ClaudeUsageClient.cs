using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace CodexBarWin.Core.Providers;

public sealed class ClaudeUsageClient : IClaudeUsageClient
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";

    private const string BetaHeader = "oauth-2025-04-20";

    private const string DefaultClaudeCodeVersion = "2.1.0";

    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30.0)
    };

    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5.0);

    private static DateTimeOffset _blockedUntil = DateTimeOffset.MinValue;

    public ClaudeUsageResponse Fetch(string accessToken)
    {
        if (DateTimeOffset.UtcNow < _blockedUntil)
        {
            throw new ClaudeFetchException(ClaudeFetchErrorKind.RateLimited, "usage endpoint is rate-limited — cooling down");
        }
        using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequestMessage.Headers.TryAddWithoutValidation("anthropic-beta", BetaHeader);
        httpRequestMessage.Headers.UserAgent.ParseAdd("claude-code/" + DefaultClaudeCodeVersion);
        HttpResponseMessage httpResponseMessage;
        try
        {
            httpResponseMessage = Http.Send(httpRequestMessage);
        }
        catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException) ? 1 : 0) != 0)
        {
            throw new ClaudeFetchException(ClaudeFetchErrorKind.Network, ex.Message);
        }
        using (httpResponseMessage)
        {
            switch (httpResponseMessage.StatusCode)
            {
                case HttpStatusCode.OK:
                    return ClaudeUsageParser.Parse(httpResponseMessage.Content.ReadAsStringAsync().GetAwaiter().GetResult()) ?? throw new ClaudeFetchException(ClaudeFetchErrorKind.Invalid, "usage response could not be parsed");
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    throw new ClaudeFetchException(ClaudeFetchErrorKind.Unauthorized, "login expired or missing user:profile scope — run `claude` to re-login");
                case HttpStatusCode.TooManyRequests:
                    _blockedUntil = DateTimeOffset.UtcNow + Cooldown;
                    throw new ClaudeFetchException(ClaudeFetchErrorKind.RateLimited, "usage endpoint is rate-limited by Anthropic — retry in a few minutes");
                default:
                    throw new ClaudeFetchException(ClaudeFetchErrorKind.Server, $"usage endpoint returned {httpResponseMessage.StatusCode}");
            }
        }
    }
}
