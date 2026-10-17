using System;

namespace CodexBarWin.Tray;

public sealed class AppSettings
{
    public const int MinIntervalSeconds = 30;

    public const int MaxIntervalSeconds = 3600;

    public int RefreshIntervalSeconds { get; set; } = 120;

    public bool CodexEnabled { get; set; } = true;

    public bool ClaudeEnabled { get; set; } = true;

    public bool CodexUseNetwork { get; set; } = true;

    public bool ClaudeUseNetwork { get; set; } = true;

    public bool LaunchAtLogin { get; set; }

    public int ClampedIntervalSeconds()
    {
        return Math.Clamp(RefreshIntervalSeconds, MinIntervalSeconds, MaxIntervalSeconds);
    }

    public AppSettings Clone()
    {
        return (AppSettings)MemberwiseClone();
    }
}
