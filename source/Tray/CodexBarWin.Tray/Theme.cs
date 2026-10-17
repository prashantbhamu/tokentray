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

	public static Color GlassTop => IsLight ? Color.FromArgb(248, 250, 254) : Color.FromArgb(35, 38, 52);

	public static Color GlassBottom => IsLight ? Color.FromArgb(232, 238, 247) : Color.FromArgb(22, 25, 36);

	public static Color GlowIndigo => Color.FromArgb(IsLight ? 25 : 48, 99, 102, 241);

	public static Color GlowTeal => Color.FromArgb(IsLight ? 20 : 36, 45, 212, 191);

	public static Color CardFill => Color.FromArgb(IsLight ? 150 : 16, 255, 255, 255);

	public static Color CardBorder => IsLight ? Color.FromArgb(28, 53, 64, 89) : Color.FromArgb(36, 255, 255, 255);

	public static Color Highlight => Color.FromArgb(IsLight ? 210 : 65, 255, 255, 255);

	public static Color Text => IsLight ? Color.FromArgb(24, 32, 48) : Color.FromArgb(247, 249, 255);

	public static Color TextDim => IsLight ? Color.FromArgb(55, 66, 86) : Color.FromArgb(208, 215, 231);

	public static Color TextFaint => IsLight ? Color.FromArgb(78, 91, 111) : Color.FromArgb(175, 186, 207);

	public static Color Track => IsLight ? Color.FromArgb(25, 40, 53, 78) : Color.FromArgb(45, 255, 255, 255);

	public static Color Good => IsLight ? Color.FromArgb(5, 135, 92) : Color.FromArgb(52, 211, 153);

	public static Color Warn => IsLight ? Color.FromArgb(153, 96, 6) : Color.FromArgb(251, 191, 36);

	public static Color Danger => IsLight ? Color.FromArgb(190, 45, 54) : Color.FromArgb(248, 113, 113);

	public static Color Muted => IsLight ? Color.FromArgb(99, 107, 122) : Color.FromArgb(165, 172, 188);

	public static Color Accent => IsLight ? Color.FromArgb(74, 80, 164) : Color.FromArgb(173, 183, 255);

	public static Color MeterColor(double fraction)
	{
		if (!(fraction >= 0.8))
		{
			if (!(fraction >= 0.5))
			{
				return Good;
			}
			return Warn;
		}
		return Danger;
	}

	public static (Color from, Color to) MeterGradient(double fraction)
	{
		if (!(fraction >= 0.8))
		{
			if (fraction >= 0.5)
			{
				return (from: Color.FromArgb(253, 211, 105), to: Color.FromArgb(245, 158, 11));
			}
			return (from: Color.FromArgb(74, 222, 167), to: Color.FromArgb(16, 185, 129));
		}
		return (from: Color.FromArgb(251, 146, 130), to: Color.FromArgb(239, 68, 68));
	}

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
