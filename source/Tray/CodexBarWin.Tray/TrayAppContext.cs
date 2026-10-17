using System;
using System.Collections.Generic;
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

	private AppSettings _settings;

	private ProviderRegistry _registry;

	private nint _iconHandle = IntPtr.Zero;

	private bool _refreshing;

	private readonly Dictionary<string, UsageSnapshot> _lastGood = new Dictionary<string, UsageSnapshot>();

	public TrayAppContext(bool open = false)
	{
		_settings = SettingsStore.Load();
		_settings.LaunchAtLogin = StartupRegistration.IsEnabled();
		_registry = BuildRegistry(_settings);
		_popup = new PopupForm(RequestRefresh);
		if (open) _popup.OpenWhenReady();
		_ = _popup.Handle;
		_tray = new NotifyIcon
		{
			Visible = true,
			Icon = SystemIcons.Application,
			Text = "CodexBarWin",
			ContextMenuStrip = BuildMenu()
		};
		_tray.MouseClick += delegate(object? _, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				_popup.ToggleNearTray();
			}
		};
		_timer = new Timer
		{
			Interval = _settings.ClampedIntervalSeconds() * 1000
		};
		_timer.Tick += delegate
		{
			RequestRefresh();
		};
		_timer.Start();
		RequestRefresh();
	}

	private static ProviderRegistry BuildRegistry(AppSettings s)
	{
		List<IProvider> list = new List<IProvider>();
		if (s.CodexEnabled)
		{
			list.Add(new CodexProvider(s.CodexUseNetwork ? new CodexUsageClient() : null));
		}
		if (s.ClaudeEnabled)
		{
			list.Add(new ClaudeProvider(s.ClaudeUseNetwork ? new ClaudeUsageClient() : null));
		}
		return new ProviderRegistry(list);
	}

	private ContextMenuStrip BuildMenu()
	{
		return new ContextMenuStrip
		{
			Items = 
			{
				{
					"Refresh now",
					(Image?)null,
					(EventHandler?)delegate
					{
						RequestRefresh();
					}
				},
				{
					"Settings…",
					(Image?)null,
					(EventHandler?)delegate
					{
						OpenSettings();
					}
				},
				(ToolStripItem)new ToolStripSeparator(),
				{
					"Quit",
					(Image?)null,
					(EventHandler?)delegate
					{
						ExitThread();
					}
				}
			}
		};
	}

	private void OpenSettings()
	{
		using SettingsForm settingsForm = new SettingsForm(_settings);
		if (settingsForm.ShowDialog() == DialogResult.OK)
		{
			ApplySettings(settingsForm.Result);
		}
	}

	private void ApplySettings(AppSettings updated)
	{
		bool num = updated.LaunchAtLogin != _settings.LaunchAtLogin;
		_settings = updated;
		SettingsStore.Save(_settings);
		if (num)
		{
			_settings.LaunchAtLogin = StartupRegistration.Set(_settings.LaunchAtLogin);
		}
		_registry = BuildRegistry(_settings);
		_timer.Interval = _settings.ClampedIntervalSeconds() * 1000;
		RequestRefresh();
	}

	private void RequestRefresh()
	{
		if (_refreshing)
		{
			return;
		}
		_refreshing = true;
		_popup.SetRefreshing(refreshing: true);
		Task.Run((Func<IReadOnlyList<UsageSnapshot>>)_registry.RefreshAll).ContinueWith(delegate(Task<IReadOnlyList<UsageSnapshot>> t)
		{
			_refreshing = false;
			try
			{
				IReadOnlyList<UsageSnapshot> readOnlyList;
				if (!t.IsFaulted && t.Result != null)
				{
					IReadOnlyList<UsageSnapshot> result = t.Result;
					readOnlyList = result;
				}
				else
				{
					IReadOnlyList<UsageSnapshot> result = Array.Empty<UsageSnapshot>();
					readOnlyList = result;
				}
				IReadOnlyList<UsageSnapshot> snapshots = readOnlyList;
				ApplyResults(snapshots);
			}
			catch (Exception value)
			{
				Diag.Log($"apply-results failed: {value}");
			}
		}, TaskScheduler.FromCurrentSynchronizationContext());
	}

	private void ApplyResults(IReadOnlyList<UsageSnapshot> snapshots)
	{
		List<UsageSnapshot> snapshots2 = snapshots.Select(KeepLastGood).ToList();
		_popup.Render(snapshots2);
		UpdateIcon(snapshots2);
	}

	private UsageSnapshot KeepLastGood(UsageSnapshot s)
	{
		if (s.Status == ProviderStatus.Ok)
		{
			_lastGood[s.ProviderId] = s;
			return s;
		}
		ProviderStatus status = s.Status;
		bool flag = (uint)(status - 3) <= 1u;
		if (flag && _lastGood.TryGetValue(s.ProviderId, out UsageSnapshot value))
		{
			string message = $"{s.Message} · showing {value.FetchedAt.ToLocalTime():HH:mm}";
			return value with
			{
				Message = message
			};
		}
		return s;
	}

	private void UpdateIcon(IReadOnlyList<UsageSnapshot> snapshots)
	{
		WeeklyIconState state = WeeklyIconState.FromSnapshots(snapshots);
		Icon icon = _tray.Icon;
		nint iconHandle = _iconHandle;
		_tray.Icon = TrayIconRenderer.Build(state, out _iconHandle);
		if (icon != null && icon != SystemIcons.Application)
		{
			icon.Dispose();
		}
		TrayIconRenderer.Destroy(iconHandle);
		_tray.Text = BuildTooltip(snapshots);
	}

	private static string BuildTooltip(IReadOnlyList<UsageSnapshot> snapshots)
	{
		string text = WeeklyIconState.FromSnapshots(snapshots).Tooltip;
		if (!string.IsNullOrEmpty(text))
		{
			if (text.Length > 63)
			{
				return text.Substring(0, 63);
			}
			return text;
		}
		return "CodexBarWin";
	}

	protected override void ExitThreadCore()
	{
		_timer.Stop();
		_tray.Visible = false;
		_tray.Dispose();
		TrayIconRenderer.Destroy(_iconHandle);
		_popup.Dispose();
		base.ExitThreadCore();
	}
}
