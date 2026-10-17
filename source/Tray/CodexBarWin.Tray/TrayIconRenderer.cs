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
    private static readonly Color Surface = Color.FromArgb(250, 250, 250);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);

    public static Icon Build(WeeklyIconState state, out nint handle)
    {
        // Match the shell's small-icon metric instead of handing it a 64px icon
        // to resample. Supersampling below keeps the arcs clean at each target DPI.
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
            // Opaque light core preserves the requested black Codex color on
            // either a light or dark Windows taskbar.
            using (var brush = new SolidBrush(Surface)) g.FillEllipse(brush, 0.5f, 0.5f, 63, 63);
            DrawRing(g, new RectangleF(3.5f, 3.5f, 57, 57), state.Codex, CodexColor);
            DrawRing(g, new RectangleF(9.5f, 9.5f, 45, 45), state.Claude, ClaudeColor);
            DrawNumber(g, state.Number, state.Leader == WeeklyLeader.Claude ? ClaudeColor
                : state.Leader == WeeklyLeader.Codex ? CodexColor : Color.FromArgb(115, 123, 135));
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

    private static void DrawRing(Graphics g, RectangleF bounds, double? fraction, Color color)
    {
        using var track = new Pen(Track, 4.6f);
        g.DrawEllipse(track, bounds);
        if (!fraction.HasValue || fraction.Value <= 0) return;
        using var progress = new Pen(color, 4.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (fraction.Value >= 1) g.DrawEllipse(progress, bounds);
        else g.DrawArc(progress, bounds, -90, (float)(360 * fraction.Value));
    }

    private static void DrawNumber(Graphics g, string text, Color color)
    {
        using var path = new GraphicsPath();
        using var family = new FontFamily("Segoe UI");
        path.AddString(text, family, (int)FontStyle.Bold, 48, PointF.Empty, StringFormat.GenericTypographic);
        var box = path.GetBounds();
        float scale = Math.Min(37f / box.Width, 31f / box.Height);
        using var transform = new Matrix(scale, 0, 0, scale,
            32 - (box.X + box.Width / 2) * scale,
            32 - (box.Y + box.Height / 2) * scale);
        path.Transform(transform);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void Destroy(nint handle) { if (handle != IntPtr.Zero) DestroyIcon(handle); }
}
