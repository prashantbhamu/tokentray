using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace CodexBarWin.Tray;

internal static class TrayIconRenderer
{
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DestroyIcon(nint handle);

	public static Icon Build(double fraction, bool hasData, out nint handle)
	{
		fraction = Math.Clamp(fraction, 0.0, 1.0);
		using Bitmap bitmap = new Bitmap(64, 64);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			graphics.Clear(Color.Transparent);
			float num = 4.5f;
			RectangleF rect = new RectangleF(num, num, 64f - num * 2f, 64f - num * 2f);
			Color color = (hasData ? MeterColor(fraction) : Color.FromArgb(120, 124, 134));
			RectangleF rect2 = RectangleF.Inflate(rect, -1.5f, -1.5f);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(235, 24, 25, 34)))
			{
				graphics.FillEllipse(brush, rect2);
			}
			using (Pen pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
			{
				graphics.DrawEllipse(pen, rect2);
			}
			using (Pen pen2 = new Pen(Color.FromArgb(55, 255, 255, 255), 6f))
			{
				graphics.DrawEllipse(pen2, rect);
			}
			if (hasData && fraction > 0.0)
			{
				float sweepAngle = (float)(360.0 * fraction);
				using Pen pen3 = new Pen(color, 6f)
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round
				};
				graphics.DrawArc(pen3, rect, -90f, sweepAngle);
			}
			string text = (hasData ? ((int)Math.Round(fraction * 100.0)).ToString() : "–");
			float pixelSize = ((text.Length >= 3) ? 30f : ((text.Length == 2) ? 40f : 46f));
			DrawCenter(graphics, text, pixelSize, hasData ? Color.White : Color.FromArgb(170, 175, 185), 64);
		}
		handle = bitmap.GetHicon();
		return Icon.FromHandle(handle);
	}

	private static void DrawCenter(Graphics g, string text, float pixelSize, Color color, int s)
	{
		using Font font = new Font("Segoe UI", pixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
		using StringFormat format = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};
		RectangleF layoutRectangle = new RectangleF(0f, -1f, s, s);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
		{
			g.DrawString(text, font, brush, new RectangleF(1f, 0.5f, s, s), format);
		}
		using SolidBrush brush2 = new SolidBrush(color);
		g.DrawString(text, font, brush2, layoutRectangle, format);
	}

	private static Color MeterColor(double f)
	{
		if (!(f >= 0.8))
		{
			if (!(f >= 0.5))
			{
				return Color.FromArgb(52, 211, 153);
			}
			return Color.FromArgb(251, 191, 36);
		}
		return Color.FromArgb(248, 113, 113);
	}

	public static void Destroy(nint handle)
	{
		if (handle != IntPtr.Zero)
		{
			DestroyIcon(handle);
		}
	}
}
