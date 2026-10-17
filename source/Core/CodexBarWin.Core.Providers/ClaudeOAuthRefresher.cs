using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodexBarWin.Core.Providers;

public sealed class ClaudeOAuthRefresher : IClaudeOAuthRefresher
{
    private const string TokenEndpoint = "https://platform.claude.com/v1/oauth/token";

    private const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30.0)
    };

    public ClaudeRefreshResult Refresh(string refreshToken)
    {
        using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://platform.claude.com/v1/oauth/token");
        httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequestMessage.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = "9d1c250a-e61b-44d9-88ed-5944d1962f5e"
        });
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
            string result = httpResponseMessage.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            switch (httpResponseMessage.StatusCode)
            {
                case HttpStatusCode.OK:
                    return ParseToken(result) ?? throw new ClaudeFetchException(ClaudeFetchErrorKind.Invalid, "token refresh response could not be parsed");
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    throw new ClaudeFetchException(ClaudeFetchErrorKind.Unauthorized, "login expired — run `claude` to re-login");
                case HttpStatusCode.TooManyRequests:
                    throw new ClaudeFetchException(ClaudeFetchErrorKind.RateLimited, "token endpoint is rate-limited — retry in a few minutes");
                default:
                    throw new ClaudeFetchException(ClaudeFetchErrorKind.Server, $"token endpoint returned {httpResponseMessage.StatusCode}");
            }
        }
    }

    internal static ClaudeRefreshResult? ParseToken(string json)
    {
        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(json);
            JsonElement rootElement = jsonDocument.RootElement;
            if (rootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            string text = Str(rootElement, "access_token");
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            JsonElement value;
            int value2;
            int val = ((rootElement.TryGetProperty("expires_in", out value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out value2)) ? value2 : 0);
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(val, 0));
            return new ClaudeRefreshResult(text, Str(rootElement, "refresh_token"), expiresAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return value.GetString();
    }
}
