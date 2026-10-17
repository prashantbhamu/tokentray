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
        // to resample. Supersampling keeps the side meters clean at each DPI.
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
            using (var brush = new SolidBrush(Surface))
            using (var square = Rounded(new RectangleF(0, 0, 64, 64), 3)) g.FillPath(brush, square);
            DrawMeter(g, new RectangleF(2, 3, 7f, 58), state.Codex, CodexColor);
            DrawMeter(g, new RectangleF(55f, 3, 7f, 58), state.Claude, ClaudeColor);
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

    private static void DrawMeter(Graphics g, RectangleF bounds, double? fraction, Color color)
    {
        using var track = new SolidBrush(Track);
        using var trackPath = Rounded(bounds, 1.5f);
        g.FillPath(track, trackPath);
        if (!fraction.HasValue || fraction.Value <= 0) return;
        float height = (float)(bounds.Height * Math.Clamp(fraction.Value, 0, 1));
        var saved = g.Save();
        g.SetClip(trackPath, CombineMode.Intersect);
        using var progress = new SolidBrush(color);
        g.FillRectangle(progress, bounds.Left, bounds.Bottom - height, bounds.Width, height);
        g.Restore(saved);
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right-d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom-d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawNumber(Graphics g, string text, Color color)
    {
        using var path = new GraphicsPath();
        using var family = new FontFamily("Segoe UI");
        path.AddString(text, family, (int)FontStyle.Bold, 48, PointF.Empty, StringFormat.GenericTypographic);
        var box = path.GetBounds();
        // Tall condensed numerals use the square interior previously lost to rings.
        float width = text == "–" ? 24 : text.Length == 1 ? 27 : 39;
        float height = text == "–" ? 6 : 47;
        float scaleX = width / box.Width, scaleY = height / box.Height;
        using var transform = new Matrix(scaleX, 0, 0, scaleY,
            32 - (box.X + box.Width / 2) * scaleX,
            32 - (box.Y + box.Height / 2) * scaleY);
        path.Transform(transform);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void Destroy(nint handle) { if (handle != IntPtr.Zero) DestroyIcon(handle); }
}
