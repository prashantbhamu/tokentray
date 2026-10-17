using System.Text.Json;

namespace CodexBarWin.Core.Providers;

public static class ClaudeUsageParser
{
    public static ClaudeUsageResponse? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(json);
            if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            JsonElement rootElement = jsonDocument.RootElement;
            return new ClaudeUsageResponse(Window(rootElement, "five_hour"), Window(rootElement, "seven_day"), Window(rootElement, "seven_day_sonnet"), Window(rootElement, "seven_day_opus"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ClaudeUsageWindow? Window(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        JsonElement value2;
        double? utilization = ((value.TryGetProperty("utilization", out value2) && value2.ValueKind == JsonValueKind.Number) ? new double?(value2.GetDouble()) : ((double?)null));
        JsonElement value3;
        string resetsAt = ((value.TryGetProperty("resets_at", out value3) && value3.ValueKind == JsonValueKind.String) ? value3.GetString() : null);
        return new ClaudeUsageWindow(utilization, resetsAt);
    }
}
