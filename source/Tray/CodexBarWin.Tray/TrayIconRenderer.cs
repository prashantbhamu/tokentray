using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace CodexBarWin.Tray;
internal static class TrayIconRenderer
{
    internal static readonly Color CodexColor = Color.FromArgb(21, 21, 21);
    internal static readonly Color ClaudeColor = Color.FromArgb(232, 119, 34);
    private static readonly Color Track = Color.FromArgb(211, 215, 221);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);
    public static Icon Build(WeeklyIconState state, out nint handle)
    {
        using var bitmap = Render(state, Math.Clamp(SystemInformation.SmallIconSize.Width, 16, 64));
        handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }
    internal static Bitmap Render(WeeklyIconState state, int size)
    {
        using var high = new Bitmap(size * 4, size * 4, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(high))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size * 4f / 64f, size * 4f / 64f);
            // Each semicircle independently represents one provider's weekly usage.
            DrawHalf(g, 266, -172, state.Codex, CodexColor);
            DrawHalf(g, 274, 172, state.Claude, ClaudeColor);
        }
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(high, new Rectangle(0, 0, size, size), 0, 0, high.Width, high.Height, GraphicsUnit.Pixel);
        }
        return bitmap;
    }
    private static void DrawHalf(Graphics g, float start, float sweep, double? fraction, Color color)
    {
        var bounds = new RectangleF(9, 9, 46, 46);
        using var track = new Pen(Track, 14);
        g.DrawArc(track, bounds, start, sweep);
        if (!fraction.HasValue || fraction.Value <= 0) return;
        using var progress = new Pen(color, 14);
        g.DrawArc(progress, bounds, start, sweep * (float)Math.Clamp(fraction.Value, 0, 1));
    }
    public static void Destroy(nint handle) { if (handle != IntPtr.Zero) DestroyIcon(handle); }
}
