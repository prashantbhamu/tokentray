using System.Drawing;
using System.Windows.Forms;

namespace CodexBarWin.Tray;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _interval;
    private readonly CheckBox _codex;
    private readonly CheckBox _codexNetwork;
    private readonly CheckBox _claude;
    private readonly CheckBox _claudeNetwork;
    private readonly CheckBox _launch;
    private readonly Font _settingsFont = new("Segoe UI", 9f);
    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Result = current.Clone();
        Text = "CodexBarWin Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = _settingsFont;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16),
            Dock = DockStyle.Fill
        };
        var intervalRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
        intervalRow.Controls.Add(new Label { Text = "Refresh interval (seconds):", AutoSize = true, Margin = new Padding(0, 5, 12, 0) });
        _interval = new NumericUpDown
        {
            Minimum = AppSettings.MinIntervalSeconds,
            Maximum = AppSettings.MaxIntervalSeconds,
            Value = current.ClampedIntervalSeconds(),
            Increment = 30,
            Width = 90,
            Margin = Padding.Empty
        };
        intervalRow.Controls.Add(_interval);
        root.Controls.Add(intervalRow);
        _codex = AddCheck(root, "Show Codex", current.CodexEnabled);
        _codexNetwork = AddCheck(root, "Allow Codex usage endpoint (network)", current.CodexUseNetwork, true);
        _claude = AddCheck(root, "Show Claude Code", current.ClaudeEnabled);
        _claudeNetwork = AddCheck(root, "Allow Claude usage endpoint (network)", current.ClaudeUseNetwork, true);
        _launch = AddCheck(root, "Launch at login", current.LaunchAtLogin);
        _codex.CheckedChanged += (_, _) => _codexNetwork.Enabled = _codex.Checked;
        _claude.CheckedChanged += (_, _) => _claudeNetwork.Enabled = _claude.Checked;
        _codexNetwork.Enabled = _codex.Checked;
        _claudeNetwork.Enabled = _claude.Checked;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 12, 0, 0)
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
        ok.Click += (_, _) => Result = new AppSettings
        {
            RefreshIntervalSeconds = (int)_interval.Value,
            CodexEnabled = _codex.Checked,
            CodexUseNetwork = _codexNetwork.Checked,
            ClaudeEnabled = _claude.Checked,
            ClaudeUseNetwork = _claudeNetwork.Checked,
            LaunchAtLogin = _launch.Checked
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons);
        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static CheckBox AddCheck(TableLayoutPanel root, string text, bool value, bool indent = false)
    {
        var check = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(indent ? 18 : 0, 4, 0, 4) };
        root.Controls.Add(check);
        return check;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _settingsFont.Dispose();
    }
}
