using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodexBarWin.Core;
using CodexBarWin.Core.Models;
using CodexBarWin.Core.Providers;

namespace CodexBarWin.Tray;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly PopupForm _popup;
    private readonly Timer _timer;
    private readonly bool _preview;
    private readonly UsageCache _cache = new();
    private AppSettings _settings;
    private ProviderRegistry _registry;
    private nint _iconHandle;
    private FileChangeSignal? _claudeLoginWatch;
    private bool _refreshing;
    private bool _refreshQueued;
    private bool _exiting;
    private int _settingsVersion;

    public TrayAppContext(bool open = false, bool preview = false)
    {
        _preview = preview;
        _settings = SettingsStore.Load();
        _settings.LaunchAtLogin = StartupRegistration.IsEnabled();
        _registry = ProviderFactory.Create(_settings, _preview);
        // The isolated preview never starts the real Claude Code CLI.
        _popup = new PopupForm(() => RequestRefresh(), _preview ? null : RenewClaudeLogin);
        if (open) _popup.OpenWhenReady();
        _ = _popup.Handle; // Install the WinForms synchronization context before refreshing.
        _tray = new NotifyIcon { Visible = true, Icon = SystemIcons.Application, Text = "TokenTray", ContextMenuStrip = BuildMenu() };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) _popup.ToggleNearTray(); };
        _timer = new Timer { Interval = _settings.ClampedIntervalSeconds() * 1000 };
        _timer.Tick += (_, _) => RequestRefresh();
        _timer.Start();
        UpdateClaudeLoginWatch();
        RequestRefresh();
    }

    public void ShowPopup() { if (!_exiting) _popup.ShowNearTray(); }

    private void RenewClaudeLogin()
    {
        if (_exiting) return;
        if (!ClaudeRenewal.TryOpen(out string? error))
            MessageBox.Show(error, "TokenTray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // Claude Code rewrites .credentials.json when it renews its login; refresh at once instead of at the next tick.
    private void UpdateClaudeLoginWatch()
    {
        _claudeLoginWatch?.Dispose();
        _claudeLoginWatch = _settings.ClaudeEnabled
            ? FileChangeSignal.TryCreate(ClaudeProvider.DefaultClaudeDir(), ".credentials.json", OnClaudeLoginChanged)
            : null;
    }

    private void OnClaudeLoginChanged()
    {
        // Runs on a thread-pool thread; refreshes must start on the UI thread.
        try { if (!_exiting && _popup.IsHandleCreated) _popup.BeginInvoke(() => RequestRefresh(queueIfBusy: true)); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Refresh now", null, (_, _) => RequestRefresh());
        menu.Items.Add(new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings()) { Enabled = !_preview });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_preview ? "Close preview" : "Quit", null, (_, _) => ExitThread());
        return menu;
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK) ApplySettings(form.Result);
    }

    private void ApplySettings(AppSettings updated)
    {
        bool startupChanged = updated.LaunchAtLogin != _settings.LaunchAtLogin;
        if (startupChanged && StartupRegistration.Set(updated.LaunchAtLogin) != updated.LaunchAtLogin)
        {
            MessageBox.Show("Windows startup could not be changed. No settings were applied.", "TokenTray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!SettingsStore.TrySave(updated, out string? error))
        {
            if (startupChanged) StartupRegistration.Set(_settings.LaunchAtLogin);
            MessageBox.Show("Settings could not be saved. " + error, "TokenTray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _settings = updated.Clone();
        _registry = ProviderFactory.Create(_settings, _preview);
        _settingsVersion++;
        _cache.Clear();
        _timer.Interval = _settings.ClampedIntervalSeconds() * 1000;
        UpdateClaudeLoginWatch();
        RequestRefresh();
    }

    private async void RequestRefresh(bool queueIfBusy = false)
    {
        if (_exiting) return;
        if (_refreshing)
        {
            // A login change during a refresh must not be lost: that refresh may have read the old login.
            if (queueIfBusy) _refreshQueued = true;
            return;
        }
        // Claude Code may be installed after TokenTray starts, and a watch stops if its folder is replaced.
        if ((_claudeLoginWatch == null || _claudeLoginWatch.Faulted) && _settings.ClaudeEnabled) UpdateClaudeLoginWatch();
        _refreshing = true;
        int version = _settingsVersion;
        var registry = _registry;
        _popup.SetRefreshing(true);
        try
        {
            var snapshots = await Task.Run(registry.RefreshAll);
            // Ignore a completed request from before settings changed, or after Quit.
            if (_exiting || version != _settingsVersion) return;
            var display = snapshots.Select(_cache.Apply).ToArray();
            _popup.Render(display);
            UpdateIcon(display);
        }
        catch (Exception ex)
        {
            Diag.Log("Refresh failed: " + ex.GetType().Name);
            if (!_exiting) _popup.SetRefreshing(false);
        }
        finally
        {
            _refreshing = false;
            bool again = version != _settingsVersion || _refreshQueued;
            _refreshQueued = false;
            if (!_exiting && again) RequestRefresh();
        }
    }

    private void UpdateIcon(UsageSnapshot[] snapshots)
    {
        var state = WeeklyIconState.FromSnapshots(snapshots);
        var oldIcon = _tray.Icon;
        nint oldHandle = _iconHandle;
        _tray.Icon = TrayIconRenderer.Build(state, out _iconHandle);
        if (oldHandle != 0) oldIcon?.Dispose();
        TrayIconRenderer.Destroy(oldHandle);
        string tooltip = (_preview ? "PREVIEW · " : "") + state.Tooltip + UsagePresentation.TooltipSuffix(snapshots);
        _tray.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
    }

    private void ReleaseResources()
    {
        if (_exiting) return;
        _exiting = true;
        _timer.Stop();
        _timer.Dispose();
        _claudeLoginWatch?.Dispose();
        _claudeLoginWatch = null;
        _tray.Visible = false;
        if (_iconHandle != 0) _tray.Icon?.Dispose();
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        TrayIconRenderer.Destroy(_iconHandle);
        _iconHandle = 0;
        _popup.Dispose();
    }

    protected override void ExitThreadCore() { ReleaseResources(); base.ExitThreadCore(); }
    protected override void Dispose(bool disposing) { if (disposing) ReleaseResources(); base.Dispose(disposing); }
}
