using System;
using System.Globalization;

namespace CodexBarWin.Core.Providers;

public sealed record ClaudeUsageWindow(double? Utilization, string? ResetsAt)
{
    public DateTimeOffset? ResetsAtOffset
    {
        get
        {
            if (!DateTimeOffset.TryParse(ResetsAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.RoundtripKind, out var result))
            {
                return null;
            }
            return result;
        }
    }
}
