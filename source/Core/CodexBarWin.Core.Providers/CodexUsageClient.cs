using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CodexBarWin.Core.Providers;

public sealed class CodexUsageClient : ICodexUsageClient
{
    private const string UsageUrl = "https://chatgpt.com/backend-api/wham/usage";

    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30.0)
    };

    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5.0);

    private static DateTimeOffset _blockedUntil = DateTimeOffset.MinValue;

    public string? LastFailure { get; private set; }

    public CodexUsageApi? Fetch(string accessToken, string? accountId)
    {
        LastFailure = null;
        if (DateTimeOffset.UtcNow < _blockedUntil)
        {
            Diag.Log($"codex: in cooldown until {_blockedUntil:HH:mm:ss}");
            return null;
        }
        try
        {
            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequestMessage.Headers.UserAgent.ParseAdd("TokenTray");
            if (!string.IsNullOrEmpty(accountId))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
            }
            using HttpResponseMessage httpResponseMessage = Http.Send(httpRequestMessage);
            if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _blockedUntil = DateTimeOffset.UtcNow + Cooldown;
                Diag.Log("codex: HTTP 429 → cooldown 5m");
                return null;
            }
            if (httpResponseMessage.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                LastFailure = "Codex login expired; run codex to sign in again";
            }
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                Diag.Log($"codex: HTTP {httpResponseMessage.StatusCode} (acct={(string.IsNullOrEmpty(accountId) ? "none" : "set")})");
                return null;
            }
            CodexUsageApi codexUsageApi = CodexUsageParser.Parse(httpResponseMessage.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            Diag.Log(((object)codexUsageApi == null) ? "codex: 200 but parse failed" : $"codex: 200 ok primary={codexUsageApi.Primary?.UsedPercent}%");
            return codexUsageApi;
        }
        catch (Exception ex)
        {
            Diag.Log("codex: " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
    }
}
