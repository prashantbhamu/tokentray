using System;
using System.Drawing;
using Microsoft.Win32;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

internal static class Theme
{
    public static bool IsLight { get; private set; } = true;

    public static void RefreshWindowsTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            IsLight = (key?.GetValue("AppsUseLightTheme") as int? ?? 1) != 0;
        }
        catch { }
    }

    // Used only by the existing screenshot-preview command; never writes Windows settings.
    internal static void Preview(bool light) => IsLight = light;

    public static Color Text => IsLight ? Color.FromArgb(24, 32, 48) : Color.FromArgb(247, 249, 255);

    public static Color TextDim => IsLight ? Color.FromArgb(55, 66, 86) : Color.FromArgb(208, 215, 231);

    public static Color TextFaint => IsLight ? Color.FromArgb(78, 91, 111) : Color.FromArgb(175, 186, 207);

    public static Color Good => IsLight ? Color.FromArgb(5, 135, 92) : Color.FromArgb(52, 211, 153);

    public static Color Warn => IsLight ? Color.FromArgb(153, 96, 6) : Color.FromArgb(251, 191, 36);

    public static Color Danger => IsLight ? Color.FromArgb(190, 45, 54) : Color.FromArgb(248, 113, 113);

    public static Color Muted => IsLight ? Color.FromArgb(99, 107, 122) : Color.FromArgb(165, 172, 188);

    public static Color Accent => IsLight ? Color.FromArgb(74, 80, 164) : Color.FromArgb(173, 183, 255);

    public static Color StatusColor(ProviderStatus status)
    {
        return status switch
        {
            ProviderStatus.Ok => Good,
            ProviderStatus.Error => Danger,
            ProviderStatus.NotInstalled => Muted,
            _ => Warn,
        };
    }

    public static string Humanize(TimeSpan t)
    {
        if (t <= TimeSpan.Zero)
        {
            return "now";
        }
        if (!(t.TotalDays >= 1.0))
        {
            if (!(t.TotalHours >= 1.0))
            {
                return $"{t.Minutes}m";
            }
            return $"{(int)t.TotalHours}h {t.Minutes}m";
        }
        return $"{(int)t.TotalDays}d {t.Hours}h";
    }
}
