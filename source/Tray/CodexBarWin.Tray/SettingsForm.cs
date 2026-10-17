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

	public AppSettings Result { get; private set; }

	public SettingsForm(AppSettings current)
	{
		Result = current.Clone();
		Text = "CodexBarWin Settings";
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.StartPosition = FormStartPosition.CenterScreen;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ClientSize = new Size(340, 248);
		Font = new Font("Segoe UI", 9f);
		int num = 16;
		int num2 = 16;
		int num3 = base.ClientSize.Width - 32;
		base.Controls.Add(new Label
		{
			Text = "Refresh interval (seconds):",
			Location = new Point(num, num2 + 2),
			AutoSize = true
		});
		_interval = new NumericUpDown
		{
			Minimum = 30m,
			Maximum = 3600m,
			Value = current.ClampedIntervalSeconds(),
			Increment = 30m,
			Location = new Point(num + num3 - 90, num2),
			Width = 90
		};
		base.Controls.Add(_interval);
		num2 += 36;
		_codex = AddCheck("Show Codex", current.CodexEnabled, num, ref num2, num3);
		_codexNetwork = AddCheck("Allow Codex usage endpoint (network)", current.CodexUseNetwork, num + 18, ref num2, num3 - 18);
		_claude = AddCheck("Show Claude Code", current.ClaudeEnabled, num, ref num2, num3);
		_claudeNetwork = AddCheck("Allow Claude usage endpoint (network)", current.ClaudeUseNetwork, num + 18, ref num2, num3 - 18);
		_launch = AddCheck("Launch at login", current.LaunchAtLogin, num, ref num2, num3);
		_codex.CheckedChanged += delegate
		{
			_codexNetwork.Enabled = _codex.Checked;
		};
		_codexNetwork.Enabled = _codex.Checked;
		_claude.CheckedChanged += delegate
		{
			_claudeNetwork.Enabled = _claude.Checked;
		};
		_claudeNetwork.Enabled = _claude.Checked;
		Button button = new Button
		{
			Text = "OK",
			DialogResult = DialogResult.OK,
			Width = 80,
			Location = new Point(base.ClientSize.Width - 176, base.ClientSize.Height - 36)
		};
		Button button2 = new Button
		{
			Text = "Cancel",
			DialogResult = DialogResult.Cancel,
			Width = 80,
			Location = new Point(base.ClientSize.Width - 88, base.ClientSize.Height - 36)
		};
		button.Click += delegate
		{
			CaptureSettings();
		};
		base.Controls.Add(button);
		base.Controls.Add(button2);
		base.AcceptButton = button;
		base.CancelButton = button2;
	}

	private CheckBox AddCheck(string text, bool value, int x, ref int y, int w)
	{
		CheckBox checkBox = new CheckBox
		{
			Text = text,
			Checked = value,
			Location = new Point(x, y),
			Width = w,
			AutoSize = false,
			Height = 24
		};
		base.Controls.Add(checkBox);
		y += 28;
		return checkBox;
	}

	private void CaptureSettings()
	{
		Result = new AppSettings
		{
			RefreshIntervalSeconds = (int)_interval.Value,
			CodexEnabled = _codex.Checked,
			CodexUseNetwork = _codexNetwork.Checked,
			ClaudeEnabled = _claude.Checked,
			ClaudeUseNetwork = _claudeNetwork.Checked,
			LaunchAtLogin = _launch.Checked
		};
	}
}
