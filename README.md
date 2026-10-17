# CodexBarWin

A compact Windows tray widget for Codex and Claude Code usage.

## Provider security and freshness

Version 1.2.0 builds the provider library from `source/Core/`, encrypts the widget's
Claude credential cache with Windows DPAPI, and clearly labels Codex local fallback
data with its original age. See [provider-fixes.md](provider-fixes.md) for evidence.
Normal launches enable automatic token refresh. `--popup-preview` remains isolated
and never rotates the main application's tokens or changes startup settings.

- Tray: outlined vertical weekly meters, black Codex left and orange Claude right, no numerals or backing tile.
- Popup: 284px-wide circular usage meters, fully opaque, matching the Windows light/dark app theme.
- Click the tray icon to toggle the popup; F5 or its refresh button updates usage; Escape dismisses it.
- No extra taskbar button. One live instance per Windows session.
- Windows startup uses a single current-user Run entry. Normal startup leaves the popup closed.

## Build and verify

Requires Windows and .NET 8 SDK. This checkout was verified using SDK 8.0.426.

```powershell
dotnet restore source/Tray/CodexBarWin.csproj
./scripts/Test-Widget.ps1 -DotNet 'C:/path/to/dotnet.exe'
dotnet publish source/Tray/CodexBarWin.csproj -c Release -o publish
./scripts/Test-Lifecycle.ps1 -Executable ./publish/CodexBarWin.exe
```

The test script checks formatting, a warning-free Release build, and 175 assertions.
Tests cover weekly-only selection, native tray sizes, provider colors, transparency,
popup/theme states, error bounds, refresh behavior, settings persistence, network policy,
startup-path validation, cached-data labeling, and DPI-aware settings layout.

Actual 1920 x 1080 Windows display tests passed at 100% (96 DPI, 16px tray) and 125%
(120 DPI, 20px tray). The original 150% setting was restored. The popup intentionally
retains its compact pixel dimensions; the settings dialog sizes to its text and controls.
See [verification.md](verification.md) and [code-review.md](code-review.md).

## Command-line controls

```text
CodexBarWin.exe --open       Open the running widget's popup, without creating a second icon
CodexBarWin.exe --quit       Gracefully close the live widget
CodexBarWin.exe --popup-preview   Isolated live preview, no settings/startup changes
CodexBarWin.exe --popup-preview --quit   Gracefully close the isolated preview
CodexBarWin.exe --status result.json     Fetch diagnostics using the configured provider/network policy
CodexBarWin.exe --shot popup.png --sample-data --preview-light
CodexBarWin.exe --shot popup-dark.png --sample-data --preview-dark
CodexBarWin.exe --shot-settings settings.png
```

Diagnostics never change Windows display settings. `CODEXBARWIN_NO_NETWORK=1` (or `true`,
case-insensitive) disables network fetches for both live mode and diagnostics. Existing
Codex local fallbacks can still be read when network is disabled.

## Code layout

- `source/Tray/CodexBarWin.Tray/`: native popup, tray rendering, refresh lifecycle, settings,
  startup registration, provider construction, caching, and diagnostics.
- `source/Core/`: source-built providers, protected Claude cache, and usage provenance.
- `bundle/CodexBarWin.Core.dll`: original provider library, retained unchanged for rollback;
  this branch no longer references it.
- `tests/WidgetTests.csproj`: native Windows regression tests; test artifacts are ignored by Git.
- `scripts/`: reproducible checks and graceful-shutdown smoke test.
- `design/` and `design-qa.md`: supplied reference and reference-comparison evidence.

## History and rollback

The provider-fix branch has been merged into `main` for version 1.2.0.
Merged design branches and superseded previews are archived
under the task's `outputs/archive/pre-cleanup-2026-10-17/`. The pre-cleanup Git bundle retains
all design branch names and commits; `git bundle list-heads PATH` lists them. The known-working
pre-cleanup executable is preserved there too. No authentication files or user documents were removed.
