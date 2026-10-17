namespace CodexBarWin.Core.Providers;

public sealed record ClaudeUsageResponse(ClaudeUsageWindow? FiveHour, ClaudeUsageWindow? SevenDay, ClaudeUsageWindow? SevenDaySonnet, ClaudeUsageWindow? SevenDayOpus);
