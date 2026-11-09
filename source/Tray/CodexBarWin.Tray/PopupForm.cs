using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using CodexBarWin.Core.Models;
using FontAwesome.Sharp;

namespace CodexBarWin.Tray;

internal sealed class PopupForm : Form
{
    private const int W = 284;
    private readonly Action _onRefresh;
    private readonly Action? _onRenewClaude;
    private readonly Font _title = new("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _header = new("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _label = new("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _body = new("Segoe UI", 13.5f, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font _percent = new("Segoe UI", 15.5f, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _footer = new("Segoe UI", 12.5f, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly IconButton _refresh;
    private readonly Button _renew;
    private readonly ToolTip _tips = new();
    private IReadOnlyList<UsageSnapshot> _snapshots = Array.Empty<UsageSnapshot>();
    private bool _refreshing = true;
    private bool _initialOpen;
    private string _updated = "Loading usage…";
    private DateTime _hiddenAt = DateTime.MinValue;
    private Rectangle _workArea;

    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x80; return p; }
    }

    public PopupForm(Action onRefresh, Action? onRenewClaude = null)
    {
        _onRefresh = onRefresh;
        _onRenewClaude = onRenewClaude;
        Theme.RefreshWindowsTheme();
        AutoScaleMode = AutoScaleMode.None;
        Text = "TokenTray";
        AccessibleName = "TokenTray usage popup";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Opacity = 1;
        ClientSize = new Size(W, 296);
        DoubleBuffered = true;
        _refresh = new IconButton
        {
            IconChar = IconChar.ArrowsRotate,
            IconSize = 24,
            Bounds = new Rectangle(W - 44, 16, 28, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            TabStop = true,
            AccessibleName = "Refresh usage",
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        _refresh.FlatAppearance.BorderSize = 0;
        _refresh.Click += (_, _) => { if (!_refreshing) _onRefresh(); };
        _tips.SetToolTip(_refresh, "Refresh usage");
        Controls.Add(_refresh);
        _renew = new Button
        {
            Text = "Open Claude Code to renew login",
            Font = _body,
            FlatStyle = FlatStyle.Flat,
            Visible = false,
            TabStop = true,
            AccessibleName = "Open Claude Code to renew the Claude login",
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        _renew.Click += (_, _) => _onRenewClaude?.Invoke();
        _tips.SetToolTip(_renew, "Opens Claude Code in a terminal so it can renew its own login. Close the terminal when usage shows again.");
        Controls.Add(_renew);
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        BackColor = Theme.IsLight ? Color.FromArgb(249, 250, 252) : Color.FromArgb(28, 31, 40);
        _refresh.IconColor = Theme.TextFaint;
        _refresh.FlatAppearance.MouseOverBackColor = Theme.IsLight ? Color.FromArgb(235, 238, 243) : Color.FromArgb(45, 49, 60);
        _renew.BackColor = Theme.IsLight ? Color.FromArgb(240, 242, 246) : Color.FromArgb(40, 44, 55);
        _renew.ForeColor = Theme.Text;
        _renew.FlatAppearance.BorderColor = TrayIconRenderer.ClaudeColor;
        _renew.FlatAppearance.MouseOverBackColor = Theme.IsLight ? Color.FromArgb(230, 233, 239) : Color.FromArgb(52, 57, 70);
        Invalidate();
    }
    internal void PreviewTheme(bool light) { Theme.Preview(light); ApplyTheme(); }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is 0x001A or 0x031A) { Theme.RefreshWindowsTheme(); ApplyTheme(); }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MeasureAndResize(); }
    protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); _hiddenAt = DateTime.UtcNow; Hide(); }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Hide(); return true; }
        if (keyData == Keys.F5) { if (!_refreshing) _onRefresh(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    public void OpenWhenReady() => _initialOpen = true;
    public void ToggleNearTray()
    {
        if (Visible) { Hide(); return; }
        if ((DateTime.UtcNow - _hiddenAt).TotalMilliseconds < 300) return;
        ShowNearTray();
    }
    public void ShowNearTray()
    {
        Theme.RefreshWindowsTheme(); ApplyTheme();
        _workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        MeasureAndResize();
        Location = PopupPlacement.BottomRight(_workArea, Size);
        Show(); Activate();
    }
    public void SetRefreshing(bool refreshing)
    {
        _refreshing = refreshing;
        _refresh.Enabled = !refreshing;
        _tips.SetToolTip(_refresh, refreshing ? "Refreshing usage…" : "Refresh usage");
        Invalidate();
    }
    public void Render(IReadOnlyList<UsageSnapshot> snapshots, bool refreshing = false)
    {
        _snapshots = snapshots;
        SetRefreshing(refreshing);
        _updated = UsagePresentation.Footer(snapshots, DateTime.Now.ToString("HH:mm"));
        if (IsHandleCreated) MeasureAndResize();
        Invalidate();
        if (_initialOpen) { _initialOpen = false; ShowNearTray(); }
    }

    private void MeasureAndResize()
    {
        using var g = CreateGraphics();
        ClientSize = new Size(W, LayoutContent(g, false));
        using var path = Rounded(new RectangleF(0, 0, Width, Height), 13);
        var old = Region; Region = new Region(path); old?.Dispose();
        if (Visible && !_workArea.IsEmpty) Location = PopupPlacement.BottomRight(_workArea, Size);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Theme.IsLight ? Color.FromArgb(225, 228, 234) : Color.FromArgb(65, 70, 83), 1);
        using var path = Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 13);
        g.DrawPath(border, path);
        LayoutContent(g, true);
    }

    private int LayoutContent(Graphics g, bool draw)
    {
        int y = 20;
        bool renew = false;
        if (_snapshots.Count == 0)
        {
            if (draw) TextAt(g, _refreshing ? "Loading usage…" : "No usage available", _body, new(22, y + 12, W - 76, 24), Theme.TextDim);
            else _renew.Visible = false;
            return 84;
        }
        for (int i = 0; i < _snapshots.Count; i++)
        {
            var s = _snapshots[i];
            bool codex = s.ProviderId == "codex";
            Color color = codex ? TrayIconRenderer.CodexColor : s.ProviderId == "claude" ? TrayIconRenderer.ClaudeColor : Theme.Accent;
            if (i > 0)
            {
                if (draw) { using var p = new Pen(Theme.IsLight ? Color.FromArgb(225, 228, 234) : Color.FromArgb(62, 67, 80)); g.DrawLine(p, 20, y, W - 20, y); }
                y += 10;
            }
            if (!codex)
            {
                if (draw) Header(g, s, 20, y, _header);
                y += 26;
            }
            if (s.Status == ProviderStatus.Ok && s.Meters.Count > 0)
            {
                foreach (var m in s.Meters)
                {
                    if (draw)
                    {
                        DrawRing(g, new Rectangle(20, y, 60, 60), m.Limit > 0 ? m.Fraction : double.NaN, color);
                        var reset = s.Resets.FirstOrDefault(r => r.Label == m.Label);
                        string resetText = reset == null ? "Reset time unavailable" : "Resets in " + Theme.Humanize(reset.TimeUntilReset);
                        if (codex)
                        {
                            TextAt(g, s.DisplayName, _title, new(98, y + 5, W - 156, 28), Theme.Text);
                            string detail = m.Label + (reset == null ? "" : " · resets in " + Theme.Humanize(reset.TimeUntilReset));
                            TextAt(g, detail, _body, new(98, y + 34, W - 114, 22), Theme.TextDim);
                        }
                        else
                        {
                            TextAt(g, m.Label, _label, new(98, y + 8, W - 118, 24), Theme.Text);
                            TextAt(g, resetText, _body, new(98, y + 34, W - 118, 22), Theme.TextDim);
                        }
                    }
                    y += 64;
                }
                if (!string.IsNullOrEmpty(s.Message))
                {
                    int h = Math.Min(72, TextRenderer.MeasureText(s.Message, _footer, new Size(W - 40, 0), TextFormatFlags.WordBreak).Height);
                    if (draw) TextAt(g, s.Message, _footer, new(20, y, W - 40, h), Theme.Warn, TextFormatFlags.WordBreak);
                    y += h + 5;
                }
            }
            else
            {
                if (codex) { if (draw) Header(g, s, 20, y, _title); y += 32; }
                string message = s.Message ?? s.Status.ToString();
                int h = Math.Min(88, TextRenderer.MeasureText(message, _body, new Size(W - 40, 0), TextFormatFlags.WordBreak).Height);
                if (draw) TextAt(g, message, _body, new(20, y, W - 40, h), Theme.StatusColor(s.Status), TextFormatFlags.WordBreak);
                y += h + 12;
                if (_onRenewClaude != null && ClaudeRenewal.Applies(s) && !renew)
                {
                    // Controls are placed only in the measure pass, never while painting.
                    if (!draw) _renew.Bounds = new Rectangle(20, y - 4, W - 40, 32);
                    renew = true;
                    y += 36;
                }
            }
            if (i < _snapshots.Count - 1) y += 12;
        }
        if (!draw) _renew.Visible = renew;
        y += 5;
        if (draw) TextAt(g, _refreshing ? "Refreshing…" : _updated, _footer, new(20, y, W - 40, 22), Theme.TextFaint);
        return y + 28;
    }
    private void Header(Graphics g, UsageSnapshot s, int x, int y, Font font)
    {
        TextAt(g, s.DisplayName, font, new(x, y, W - x - 20, 27), Theme.Text);
        if (s.Plan == null) return;
        int width = TextRenderer.MeasureText(g, s.DisplayName, font, Size.Empty, TextFormatFlags.NoPadding).Width;
        if (width >= W - x - 36) return;
        TextAt(g, "·  " + s.Plan, _body, new(x + width + 8, y + 3, W - x - width - 28, 24), Theme.Accent);
    }
    private void DrawRing(Graphics g, Rectangle r, double fraction, Color color)
    {
        double value = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
        var bounds = new RectangleF(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
        using var track = new Pen(Theme.IsLight ? Color.FromArgb(228, 230, 235) : Color.FromArgb(81, 87, 102), 5);
        g.DrawEllipse(track, bounds);
        if (value > 0)
        {
            using var progress = new Pen(color, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (value >= 1) g.DrawEllipse(progress, bounds); else g.DrawArc(progress, bounds, -90, (float)(value * 360));
        }
        TextAt(g, FormatFraction(fraction), _percent, r, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
    internal static string FormatFraction(double fraction) => double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1).ToString("P0") : "—";
    private static void TextAt(Graphics g, string text, Font font, Rectangle rect, Color color, TextFormatFlags flags = TextFormatFlags.Default)
        => TextRenderer.DrawText(g, text, font, rect, color, flags | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _title.Dispose(); _header.Dispose(); _label.Dispose(); _body.Dispose(); _percent.Dispose(); _footer.Dispose(); _tips.Dispose(); }
        base.Dispose(disposing);
    }
}
