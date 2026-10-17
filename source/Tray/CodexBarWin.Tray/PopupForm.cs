using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

internal sealed class PopupForm : Form
{
	private const int W = 324;

	private const int Pad = 16;

	private const int CardGap = 12;

	private const int CardPad = 13;

	private const int Corner = 16;

	private readonly Action _onRefresh;

	private readonly Font _fHeader = new Font("Segoe UI", 15.5f, FontStyle.Bold, GraphicsUnit.Pixel);

	private readonly Font _fLabel = new Font("Segoe UI", 13.5f, FontStyle.Regular, GraphicsUnit.Pixel);

	private readonly Font _fPct = new Font("Segoe UI", 14.5f, FontStyle.Bold, GraphicsUnit.Pixel);

	private readonly Font _fSmall = new Font("Segoe UI", 12.5f, FontStyle.Regular, GraphicsUnit.Pixel);

	private readonly Font _fBtn = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);

	private IReadOnlyList<UsageSnapshot> _snapshots = Array.Empty<UsageSnapshot>();

	private bool _refreshing = true;

	private bool _initialOpen;

	public void OpenWhenReady() => _initialOpen = true;

	private string _updated = "";

	private Rectangle _refreshRect;

	private DateTime _hiddenAt = DateTime.MinValue;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 128;
			return createParams;
		}
	}

	public PopupForm(Action onRefresh)
	{
		_onRefresh = onRefresh;
		Theme.RefreshWindowsTheme();
		AutoScaleMode = AutoScaleMode.None;
		Text = "CodexBarWin";
		AccessibleName = "CodexBarWin usage popup";
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.StartPosition = FormStartPosition.Manual;
		BackColor = Theme.GlassBottom;
		base.Opacity = 1.0;
		base.Width = 324;
		base.Height = 200;
		DoubleBuffered = true;
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		MeasureAndResize();
	}

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (m.Msg == 0x001A || m.Msg == 0x031A) // Windows settings/theme changed
		{
			Theme.RefreshWindowsTheme();
			BackColor = Theme.GlassBottom;
			Invalidate();
		}
	}

	protected override void OnDeactivate(EventArgs e)
	{
		base.OnDeactivate(e);
		_hiddenAt = DateTime.UtcNow;
		Hide();
	}

	public void ToggleNearTray()
	{
		Theme.RefreshWindowsTheme();
		BackColor = Theme.GlassBottom;
		if (base.Visible)
		{
			Hide();
		}
		else if (!((DateTime.UtcNow - _hiddenAt).TotalMilliseconds < 300.0))
		{
			MeasureAndResize();
			Rectangle rectangle = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
			base.Location = new Point(rectangle.Right - base.Width - 12, rectangle.Bottom - base.Height - 12);
			Show();
			Activate();
		}
	}

	public void SetRefreshing(bool refreshing)
	{
		_refreshing = refreshing;
		Invalidate();
	}

	public void Render(IReadOnlyList<UsageSnapshot> snapshots, bool refreshing = false)
	{
		_snapshots = snapshots;
		_refreshing = refreshing;
		_updated = (refreshing ? "Refreshing…" : $"Used · updated {DateTime.Now:HH:mm}");
		if (base.IsHandleCreated)
		{
			MeasureAndResize();
		}
		Invalidate();
		if (_initialOpen) { _initialOpen = false; ToggleNearTray(); }
	}

	private void MeasureAndResize()
	{
		using Graphics g = CreateGraphics();
		int height = LayoutContent(g, draw: false);
		base.ClientSize = new Size(324, height);
		ApplyRoundedRegion();
	}

	private void ApplyRoundedRegion()
	{
		Region region = base.Region;
		using GraphicsPath path = Rounded(new RectangleF(0f, 0f, base.Width, base.Height), 16f);
		base.Region = new Region(path);
		region?.Dispose();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		PaintBackground(e.Graphics);
		LayoutContent(e.Graphics, draw: true);
	}

	private void PaintBackground(Graphics g)
	{
		RectangleF rectangleF = new RectangleF(0f, 0f, base.Width, base.Height);
		using GraphicsPath path = Rounded(rectangleF, 16f);
		Region clip = g.Clip;
		g.SetClip(path, CombineMode.Replace);
		using (LinearGradientBrush brush = new LinearGradientBrush(rectangleF, Theme.GlassTop, Theme.GlassBottom, LinearGradientMode.Vertical))
		{
			g.FillRectangle(brush, rectangleF);
		}
		Glow(g, new PointF((float)base.Width * 0.16f, 36f), (float)base.Width * 0.62f, Theme.GlowIndigo);
		Glow(g, new PointF((float)base.Width * 0.86f, (float)base.Height * 0.92f), (float)base.Width * 0.62f, Theme.GlowTeal);
		using (Pen pen = new Pen(Theme.Highlight, 1f))
		{
			using GraphicsPath path2 = Rounded(new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f), 16f);
			g.DrawPath(pen, path2);
		}
		g.Clip = clip;
	}

	private int LayoutContent(Graphics g, bool draw)
	{
		int num = 292;
		int num2 = num - 26;
		int num3 = 16;
		IReadOnlyList<UsageSnapshot> readOnlyList = ((_snapshots.Count == 0 && _refreshing) ? null : _snapshots);
		if (readOnlyList == null)
		{
			int num4 = 22;
			if (draw)
			{
				DrawText(g, "Loading…", _fLabel, new Rectangle(29, num3 + 6, num2, 20), Theme.TextDim);
			}
			num3 += num4 + 12;
		}
		else
		{
			foreach (UsageSnapshot item in readOnlyList)
			{
				int num5 = MeasureCard(g, item, num2);
				if (draw)
				{
					DrawCard(g, item, new Rectangle(16, num3, num, num5), num2);
				}
				num3 += num5 + 12;
			}
			if (readOnlyList.Count > 0)
			{
				num3 -= 12;
			}
		}
		num3 += 10;
		if (draw)
		{
			DrawText(g, _updated, _fSmall, new Rectangle(18, num3, num - 100, 24), Theme.TextFaint, TextFormatFlags.VerticalCenter);
			_refreshRect = new Rectangle(216, num3 - 2, 92, 28);
			DrawPill(g, _refreshRect, "Refresh");
		}
		return num3 + 44;
	}

	private int MeasureCard(Graphics g, UsageSnapshot s, int contentW)
	{
		int num = 22;
		if (s.Status == ProviderStatus.Ok && s.Meters.Count > 0)
		{
			num += s.Meters.Count * 52 - 8;
			if (s.Message != null)
			{
				num += 6 + MessageHeight(g, s.Message, _fSmall, contentW);
			}
		}
		else
		{
			num += 4 + MessageHeight(g, StatusText(s), _fLabel, contentW);
		}
		return 26 + num;
	}

	private void DrawCard(Graphics g, UsageSnapshot s, Rectangle card, int contentW)
	{
		using (GraphicsPath path = Rounded(new RectangleF(card.X, card.Y, card.Width, card.Height), 12f))
		{
			using (SolidBrush brush = new SolidBrush(Theme.CardFill))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Theme.CardBorder, 1f);
			g.DrawPath(pen, path);
		}
		using (LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(card.X + 14, card.Y, card.Width - 28, 1f), Theme.Highlight, Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Horizontal))
		{
			g.FillRectangle(brush2, card.X + 14, card.Y + 1, card.Width - 28, 1);
		}
		int num = card.X + 13;
		int num2 = card.Y + 13;
		Color color = Theme.StatusColor(s.Status);
		using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(70, color)))
		{
			g.FillEllipse(brush3, num - 2, num2 + 3, 14, 14);
		}
		using (SolidBrush brush4 = new SolidBrush(color))
		{
			g.FillEllipse(brush4, num + 1, num2 + 6, 8, 8);
		}
		int num3 = num + 18;
		Size size = TextRenderer.MeasureText(g, s.DisplayName, _fHeader, Size.Empty, TextFormatFlags.NoPadding);
		DrawText(g, s.DisplayName, _fHeader, new Rectangle(num3, num2, size.Width + 2, 20), Theme.Text);
		if (s.Plan != null)
		{
			DrawText(g, "·  " + s.Plan, _fLabel, new Rectangle(num3 + size.Width + 8, num2 + 2, contentW, 18), Theme.Accent);
		}
		num2 += 22;
		if (s.Status == ProviderStatus.Ok && s.Meters.Count > 0)
		{
			foreach (Meter m in s.Meters)
			{
				DrawText(g, m.Label, _fLabel, new Rectangle(num, num2, contentW - 56, 19), Theme.TextDim);
				DrawText(g, m.Fraction.ToString("P0"), _fPct, new Rectangle(num + contentW - 56, num2, 56, 19), Theme.Text, TextFormatFlags.Right);
				Color meterColor = s.ProviderId == "codex" ? TrayIconRenderer.CodexColor
					: s.ProviderId == "claude" ? TrayIconRenderer.ClaudeColor : Theme.MeterColor(m.Fraction);
				DrawMeter(g, new Rectangle(num, num2 + 21, contentW, 7), m.Fraction, meterColor);
				ResetWindow resetWindow = s.Resets.FirstOrDefault((ResetWindow r) => r.Label == m.Label);
				if ((object)resetWindow != null)
				{
					DrawText(g, "resets in " + Theme.Humanize(resetWindow.TimeUntilReset), _fSmall, new Rectangle(num, num2 + 31, contentW, 18), Theme.TextFaint);
				}
				num2 += 52;
			}
			if (s.Message != null)
			{
				num2 -= 2;
				DrawText(g, s.Message, _fSmall, new Rectangle(num, num2, contentW, MessageHeight(g, s.Message, _fSmall, contentW)), Theme.Warn, TextFormatFlags.WordBreak);
			}
		}
		else
		{
			DrawText(g, StatusText(s), _fLabel, new Rectangle(num, num2 + 2, contentW, MessageHeight(g, StatusText(s), _fLabel, contentW)), Theme.StatusColor(s.Status), TextFormatFlags.WordBreak);
		}
	}

	private void DrawMeter(Graphics g, Rectangle r, double fraction, Color color)
	{
		fraction = Math.Clamp(fraction, 0.0, 1.0);
		float num = (float)r.Height / 2f;
		using (SolidBrush brush = new SolidBrush(Theme.Track))
		{
			using GraphicsPath path = Rounded(r, num);
			g.FillPath(brush, path);
		}
		int num2 = (int)Math.Round(fraction * (double)r.Width);
		if (num2 < r.Height)
		{
			return;
		}
		Rectangle rectangle = new Rectangle(r.X, r.Y, num2, r.Height);
		using SolidBrush brush3 = new SolidBrush(color);
		using GraphicsPath path3 = Rounded(rectangle, num);
		g.FillPath(brush3, path3);
	}

	private void DrawPill(Graphics g, Rectangle r, string text)
	{
		using (GraphicsPath path = Rounded(r, (float)r.Height / 2f))
		{
			using (SolidBrush brush = new SolidBrush(_refreshing ? Color.FromArgb(28, 129, 140, 248) : Color.FromArgb(60, 129, 140, 248)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Color.FromArgb(90, 129, 140, 248), 1f);
			g.DrawPath(pen, path);
		}
		DrawText(g, text, _fBtn, r, _refreshing ? Theme.TextFaint : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}

	private void OnPillClick(MouseEventArgs e)
	{
		if (!_refreshing && _refreshRect.Contains(e.Location))
		{
			_onRefresh();
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		OnPillClick(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		Cursor = ((_refreshRect.Contains(e.Location) && !_refreshing) ? Cursors.Hand : Cursors.Default);
	}

	private static void DrawText(Graphics g, string text, Font font, Rectangle rect, Color color, TextFormatFlags flags = TextFormatFlags.Default)
	{
		TextRenderer.DrawText(g, text, font, rect, color, flags | TextFormatFlags.NoPadding);
	}

	private static int MessageHeight(Graphics g, string text, Font font, int width)
	{
		return TextRenderer.MeasureText(g, text, font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
	}

	private static string StatusText(UsageSnapshot s)
	{
		if (s.Message != null)
		{
			return $"{s.Status}: {s.Message}";
		}
		return s.Status.ToString();
	}

	private static void Glow(Graphics g, PointF center, float radius, Color color)
	{
		using GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddEllipse(center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
		PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
		pathGradientBrush.CenterColor = color;
		pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, color) };
		pathGradientBrush.CenterPoint = center;
		using PathGradientBrush brush = pathGradientBrush;
		g.FillPath(brush, graphicsPath);
	}

	private static GraphicsPath Rounded(RectangleF r, float radius)
	{
		float num = radius * 2f;
		GraphicsPath graphicsPath = new GraphicsPath();
		if (radius <= 0f)
		{
			graphicsPath.AddRectangle(r);
			return graphicsPath;
		}
		graphicsPath.AddArc(r.Left, r.Top, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Top, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.Left, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_fHeader.Dispose();
			_fLabel.Dispose();
			_fPct.Dispose();
			_fSmall.Dispose();
			_fBtn.Dispose();
		}
		base.Dispose(disposing);
	}
}
