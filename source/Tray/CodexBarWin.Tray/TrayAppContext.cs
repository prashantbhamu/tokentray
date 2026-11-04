using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodexBarWin.Core;
using CodexBarWin.Core.Models;

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
    private bool _refreshing;
    private bool _exiting;
    private int _settingsVersion;

    public TrayAppContext(bool open = false, bool preview = false)
    {
        _preview = preview;
        _settings = SettingsStore.Load();
        _settings.LaunchAtLogin = StartupRegistration.IsEnabled();
        _registry = ProviderFactory.Create(_settings, _preview);
        _popup = new PopupForm(RequestRefresh);
        if (open) _popup.OpenWhenReady();
        _ = _popup.Handle; // Install the WinForms synchronization context before refreshing.
        _tray = new NotifyIcon { Visible = true, Icon = SystemIcons.Application, Text = "TokenTray", ContextMenuStrip = BuildMenu() };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) _popup.ToggleNearTray(); };
        _timer = new Timer { Interval = _settings.ClampedIntervalSeconds() * 1000 };
        _timer.Tick += (_, _) => RequestRefresh();
        _timer.Start();
        RequestRefresh();
    }

    public void ShowPopup() { if (!_exiting) _popup.ShowNearTray(); }

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
        RequestRefresh();
    }

    private async void RequestRefresh()
    {
        if (_exiting || _refreshing) return;
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
            if (!_exiting && version != _settingsVersion) RequestRefresh();
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
