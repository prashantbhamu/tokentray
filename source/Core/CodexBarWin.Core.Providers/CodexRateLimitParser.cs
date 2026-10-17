using System;
using System.Text.Json;

namespace CodexBarWin.Core.Providers;

public static class CodexRateLimitParser
{
    public static CodexRateLimits? Parse(string? logBody)
    {
        if (string.IsNullOrEmpty(logBody))
        {
            return null;
        }
        string text = ExtractBalancedObject(logBody, "\"rate_limits\"");
        if (text == null)
        {
            return null;
        }
        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(text);
            JsonElement rootElement = jsonDocument.RootElement;
            bool allowed = GetBool(rootElement, "allowed", fallback: true);
            bool limitReached = GetBool(rootElement, "limit_reached", fallback: false);
            return new CodexRateLimits(allowed, limitReached, ParseWindow(rootElement, "primary"), ParseWindow(rootElement, "secondary"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CodexWindow? ParseWindow(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return new CodexWindow(GetDouble(value, "used_percent", 0.0), (int)GetLong(value, "window_minutes", 0L), GetLong(value, "reset_after_seconds", 0L), GetLong(value, "reset_at", 0L));
    }

    internal static string? ExtractBalancedObject(string text, string key)
    {
        int num = text.IndexOf(key, StringComparison.Ordinal);
        if (num < 0)
        {
            return null;
        }
        int num2 = text.IndexOf('{', num + key.Length);
        if (num2 < 0)
        {
            return null;
        }
        int num3 = 0;
        bool flag = false;
        bool flag2 = false;
        for (int i = num2; i < text.Length; i++)
        {
            char c = text[i];
            if (flag)
            {
                if (flag2)
                {
                    flag2 = false;
                    continue;
                }
                switch (c)
                {
                    case '\\':
                        flag2 = true;
                        break;
                    case '"':
                        flag = false;
                        break;
                }
                continue;
            }
            switch (c)
            {
                case '"':
                    flag = true;
                    break;
                case '{':
                    num3++;
                    break;
                case '}':
                    num3--;
                    if (num3 == 0)
                    {
                        return text.Substring(num2, i - num2 + 1);
                    }
                    break;
            }
        }
        return null;
    }

    private static bool GetBool(JsonElement e, string name, bool fallback)
    {
        if (!e.TryGetProperty(name, out var value) || (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            return fallback;
        }
        return value.GetBoolean();
    }

    private static long GetLong(JsonElement e, string name, long fallback)
    {
        if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var value2))
        {
            return fallback;
        }
        return value2;
    }

    private static double GetDouble(JsonElement e, string name, double fallback)
    {
        if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var value2))
        {
            return fallback;
        }
        return value2;
    }
}
