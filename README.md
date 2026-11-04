# TokenTray

A compact Windows tray widget that shows your Codex and Claude Code usage limits.

- **Tray icon:** two vertical bars, black for Codex and orange for Claude, filling with weekly usage.
  Hover for the exact percentages.
- **Popup:** click the icon for session and weekly rings with reset countdowns, in the Windows
  light or dark theme. F5 or the refresh button updates it; Escape closes it.
- Single instance, no taskbar button, optional launch at login.

> **Unofficial.** TokenTray is not affiliated with or endorsed by OpenAI or Anthropic. It reads
> usage from the same undocumented endpoints the official tools use, which can change or stop
> working at any time. Use it at your own risk.

## Download and run

Download **TokenTray.exe** from the [latest release](https://github.com/prashantbhamu/tokentray/releases/latest).
It is a self-contained, portable Windows x64 app; no installer or .NET runtime is needed. Keep it in
a permanent folder, run it, and turn on **Launch at login** from the tray menu's Settings if you want.
The executable is unsigned, so Windows SmartScreen may warn on first run.

You must already be signed in to Codex and/or Claude Code on the same PC.

## How it gets your usage, and what it touches

TokenTray only **reads** the logins the official CLIs already store. It never asks for a password
and never sends your credentials anywhere except the provider's own usage endpoint over HTTPS.

| Provider | Reads | Calls |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json`, session logs under `.codex\sessions`, `logs_2.sqlite` | `https://chatgpt.com/backend-api/wham/usage` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `https://api.anthropic.com/api/oauth/usage` |

- **Claude logins are never refreshed by TokenTray.** Refreshing would rotate the refresh token
  that Claude Code itself relies on and sign the CLI out. When the Claude login expires, the
  popup says so; opening Claude Code once refreshes it and TokenTray picks it up.
- **Claude Code's current login is the only source.** Switching or signing out of accounts in
  Claude Code is reflected immediately; TokenTray keeps no copy of the login that could show a
  previous account's usage.
- The Claude usage request identifies itself with a Claude Code user agent, because the
  endpoint is built for Claude Code.
- If the Codex endpoint is unreachable or its login has expired, TokenTray falls back to Codex's
  local logs and labels that data with its original age (and the reason live data is missing).
- TokenTray writes no credentials. Versions before 1.3.0 kept a DPAPI-encrypted Claude login
  cache in `%APPDATA%\CodexBarWin\`; 1.3.0 no longer reads or updates it, and it can be deleted.
- Set `CODEXBARWIN_NO_NETWORK=1` (or `true`) to disable all network calls; the per-provider
  network switches in Settings do the same individually.

## Build and test

Requires Windows and the .NET 8 SDK.

```powershell
dotnet restore source/Tray/CodexBarWin.csproj
./scripts/Test-Widget.ps1
dotnet publish source/Tray/CodexBarWin.csproj -c Release -o publish
./scripts/Test-Lifecycle.ps1 -Executable ./publish/TokenTray.exe
```

`Test-Widget.ps1` checks formatting, builds Release with warnings as errors, and runs the
regression suite (tray rendering, popup layout and themes, refresh and caching behavior,
settings, startup registration, and the provider/credential handling above). All credentials
used by the tests are fake.

## Command line

```text
TokenTray.exe --open                    Open the running widget's popup
TokenTray.exe --quit                    Close the running widget
TokenTray.exe --popup-preview           Isolated preview instance (no settings or startup changes)
TokenTray.exe --status result.json      Write current usage diagnostics as JSON
TokenTray.exe --shot popup.png --sample-data [--preview-light|--preview-dark]
TokenTray.exe --shot-settings settings.png
```

Set `CODEXBARWIN_DEBUG=1` to write a diagnostic log to `%APPDATA%\CodexBarWin\debug.log`
(status messages only; no tokens are logged).

## Code layout

- `source/Tray/`: tray icon, popup, settings, startup registration, refresh lifecycle, diagnostics.
- `source/Core/`: Codex and Claude providers, credential store, usage parsing.
- `tests/`: Windows regression tests.
- `scripts/`: build/test and graceful-shutdown checks.
- `design/`: design reference and comparison renders.

The project started as *CodexBarWin*. Internal namespaces, the settings folder
(`%APPDATA%\CodexBarWin`), environment variables and the startup entry keep that name so
existing installs keep their settings.
