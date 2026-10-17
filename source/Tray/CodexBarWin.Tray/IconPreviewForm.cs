using System;
using System.Drawing;
using System.Windows.Forms;

namespace CodexBarWin.Tray;

internal sealed class IconPreviewForm : Form
{
    private readonly NotifyIcon _testTray = new() { Visible = false };
    private nint _testHandle;
    private readonly Label _testStatus = new() { Width = 650, Height = 42, Margin = new Padding(4, 4, 4, 4), Text = "No test tray icon active. Closing this window removes any preview icon." };
    public IconPreviewForm()
    {
        Text = "CodexBarWin — side-meter branch test";
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        BackColor = Color.FromArgb(245, 247, 250);
        ClientSize = new Size(740, 510);
        StartPosition = FormStartPosition.CenterScreen;
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };
        Controls.Add(flow);
        flow.Controls.Add(new Label { Text = "BRANCH PREVIEW · Installed widget unchanged", AutoSize = false,
            Width = 650, Height = 32, Font = new Font(Font, FontStyle.Bold) });
        AddCase(flow, "Codex higher", new(0.40, 0.19));
        AddCase(flow, "Claude higher", new(0.40, 0.60));
        AddCase(flow, "At the limit", new(1.0, 0.20));
        flow.Controls.Add(new Label { Text = "Left meter: Codex (black) · Right meter: Claude (orange)\nCenter: higher weekly usage, colored for that provider.\nSmall row: actual 16 / 20 / 24 / 32 pixel renderings. Larger icon above is magnified.",
            Width = 650, Height = 72, Margin = new Padding(4, 12, 4, 4) });
        flow.Controls.Add(_testStatus);
    }

    private void AddCase(FlowLayoutPanel flow, string title, WeeklyIconState state)
    {
        var group = new GroupBox { Text = title, Width = 210, Height = 280, Margin = new Padding(3, 3, 10, 3) };
        var image = TrayIconRenderer.Render(state, 128);
        var picture = new PictureBox { Image = image, Location = new Point(40, 28), Size = new Size(128, 128) };
        picture.Disposed += (_, _) => image.Dispose();
        group.Controls.Add(picture);
        group.Controls.Add(new Label { Text = $"Codex {state.Codex:P0} · Claude {state.Claude:P0}",
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(8, 158, 194, 26) });
        int x = 35;
        foreach (int size in new[] { 16, 20, 24, 32 })
        {
            var small = TrayIconRenderer.Render(state, size);
            var smallPicture = new PictureBox { Image = small, Bounds = new Rectangle(x, 192 + (32 - size) / 2, size, size) };
            smallPicture.Disposed += (_, _) => small.Dispose();
            group.Controls.Add(smallPicture);
            x += size + 9;
        }
        var tryButton = new Button { Text = $"Try {state.Number} in tray", Bounds = new Rectangle(30, 235, 150, 30) };
        tryButton.Click += (_, _) => SetPreview(state);
        group.Controls.Add(tryButton);
        flow.Controls.Add(group);
    }

    private void SetPreview(WeeklyIconState state)
    {
        var oldIcon = _testTray.Icon;
        var oldHandle = _testHandle;
        _testTray.Icon = TrayIconRenderer.Build(state, out _testHandle);
        _testTray.Text = "TEST · " + state.Tooltip;
        _testTray.Visible = true;
        oldIcon?.Dispose();
        TrayIconRenderer.Destroy(oldHandle);
        _testStatus.Text = $"Test icon active: {state.Number}, {state.Leader} color. Sample data only; installed widget unchanged.";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _testTray.Visible = false;
            _testTray.Icon?.Dispose();
            _testTray.Dispose();
            TrayIconRenderer.Destroy(_testHandle);
        }
        base.Dispose(disposing);
    }
}
