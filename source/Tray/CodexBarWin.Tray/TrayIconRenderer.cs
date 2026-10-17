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
            // Same 14-unit thickness as the split ring, with independent bottom-up fill.
            DrawBar(g, 10, state.Codex, CodexColor);
            DrawBar(g, 40, state.Claude, ClaudeColor);
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
    private static void DrawBar(Graphics g, float x, double? fraction, Color color)
    {
        const float top = 2, height = 60, width = 14, radius = 3;
        using var path = new GraphicsPath();
        path.AddArc(x, top, radius * 2, radius * 2, 180, 90);
        path.AddArc(x + width - radius * 2, top, radius * 2, radius * 2, 270, 90);
        path.AddArc(x + width - radius * 2, top + height - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(x, top + height - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        using var track = new SolidBrush(Track);
        g.FillPath(track, path);
        if (!fraction.HasValue || fraction.Value <= 0) return;
        var saved = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        float filled = height * (float)Math.Clamp(fraction.Value, 0, 1);
        using var progress = new SolidBrush(color);
        g.FillRectangle(progress, x, top + height - filled, width, filled);
        g.Restore(saved);
    }
    public static void Destroy(nint handle) { if (handle != IntPtr.Zero) DestroyIcon(handle); }
}
