using System.Text.Json;

namespace CodexBarWin.Core.Providers;

public static class CodexUsageParser
{
    public static CodexUsageApi? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(json);
            JsonElement rootElement = jsonDocument.RootElement;
            if (rootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            JsonElement value;
            string text = ((rootElement.TryGetProperty("plan_type", out value) && value.ValueKind == JsonValueKind.String) ? value.GetString() : null);
            CodexApiWindow codexApiWindow = null;
            CodexApiWindow codexApiWindow2 = null;
            if (rootElement.TryGetProperty("rate_limit", out var value2) && value2.ValueKind == JsonValueKind.Object)
            {
                codexApiWindow = Window(value2, "primary_window");
                codexApiWindow2 = Window(value2, "secondary_window");
            }
            if (text == null && (object)codexApiWindow == null && (object)codexApiWindow2 == null)
            {
                return null;
            }
            return new CodexUsageApi(text, codexApiWindow, codexApiWindow2);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CodexApiWindow? Window(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return new CodexApiWindow(GetDouble(value, "used_percent"), GetLong(value, "reset_at"), GetInt(value, "limit_window_seconds"));
    }

    private static double GetDouble(JsonElement e, string n)
    {
        if (!e.TryGetProperty(n, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var value2))
        {
            return 0.0;
        }
        return value2;
    }

    private static int GetInt(JsonElement e, string n)
    {
        if (!e.TryGetProperty(n, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var value2))
        {
            return 0;
        }
        return value2;
    }

    private static long GetLong(JsonElement e, string n)
    {
        if (!e.TryGetProperty(n, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var value2))
        {
            return 0L;
        }
        return value2;
    }
}
