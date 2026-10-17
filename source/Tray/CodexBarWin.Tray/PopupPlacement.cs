using System;
using System.Drawing;

namespace CodexBarWin.Tray;

internal static class PopupPlacement
{
    public static Point BottomRight(Rectangle workArea, Size popup)
    {
        const int margin = 12;
        return new Point(Math.Max(workArea.Left, workArea.Right - popup.Width - margin),
            Math.Max(workArea.Top, workArea.Bottom - popup.Height - margin));
    }
}
