# Provider security and freshness fixes

Branch: `fix/provider-security-and-freshness`, based on main `8d67a58`.
Merged into main and installed as version 1.2.0 on 17 October 2026.
The evidence below describes the isolated branch tests before promotion.

## Changes

1. Claude widget cache now uses Windows DPAPI CurrentUser encryption in
   `claude-token.dpapi`, never a new plaintext JSON fallback. Writes are encrypted,
   round-trip verified, flushed, and atomically replaced. Production migration only
   retires the widget's old same-directory JSON after a protected copy succeeds.
   CLI credentials remain read-only. Failure preserves usable credentials in memory,
   retains recoverable prior data, and surfaces a warning. Token record diagnostics
   redact secrets. DPAPI protects data at rest, not against malware already running
   as the same Windows user.
2. Codex local-session and SQLite fallbacks carry their original event timestamp
   and explicit local provenance. Fetch time and file modification time are not
   presented as data freshness. Missing event time displays 'time unknown'; older
   than 30 minutes displays 'stale'. Popup notes, footer, and tooltip distinguish
   local/cached data from live API results. Cached failures preserve source age.

The formerly binary-only Core is source-built on this branch. The original DLL
remains unchanged and unreferenced for rollback.

## Original branch side-by-side safety (before promotion)

All branch launches use the separate ProviderFixPreview identity and disabled
settings/startup controls. The encrypted real-login copy is stored under
`%APPDATA%/CodexBarWin/provider-fixes-preview/`. Existing CLI and main-widget
credentials are only read, never changed or retired by the preview. Real OAuth
rotation is deliberately disabled here so an old running main app cannot lose
its refresh token. Expired credentials or a 401 yield an explicit preview-policy
message, not an attempted real rotation. Main still has the old behavior until
the user approves promotion; these fixes are not yet applied globally.

## Evidence

- Release build: zero warnings, zero errors.
- 174 automated assertions pass, including DPAPI round trip, no readable fake
  tokens, safe legacy migration, corrupt-cache recovery, failed encryption,
  failed verification, read-only legacy retirement, scope preservation,
  expired-token refresh, 401 refresh-and-retry, restart reuse, and in-memory
  recovery from save failure. Credential fixtures are fake and isolated.
- Codex tests cover old event/new file mtime, missing time, recent-but-local
  data, whitespace in JSON, SQLite event timestamps, live API provenance, and
  UI/cache labeling.
- Real API check at 22:15 IST: Codex weekly 50%; Claude session 24%, weekly 22%.
  Both status OK, source Live, no warning. A second real fetch for popup capture
  also succeeded and retained the approved compact design.
- Existing Claude CLI credential file and installed Downloads executable had
  identical SHA-256 before/after. The startup entry still targets the installed
  Downloads executable. The original main process remained running.
- The real preview cache is a 658-byte DPAPI file; no JSON cache was created in
  its directory. Real token contents were not printed or committed.
- Graceful preview shutdown with refresh potentially in flight passes.
- NuGet audit reports no known vulnerable packages in Core's dependency graph.

Actual OAuth token rotation was tested with injected fake refresh/usage clients,
not forced against the real account. Live authenticated API reads and reopening
the encrypted preview cache were exercised against the existing login.

## Reproduce on current main

```powershell
./scripts/Test-Widget.ps1 -Output test-results-production
dotnet publish source/Tray/CodexBarWin.csproj -c Release -o publish
./scripts/Test-Lifecycle.ps1 -Executable ./publish/CodexBarWin.exe
./publish/CodexBarWin.exe --popup-preview
```

The explicit popup-preview mode can run beside main and uses the same approved
widget UI. Do not register a preview for startup. Production diagnostics
(`--status result.json`) permit real token rotation: avoid simultaneous diagnostic
and app requests around token expiry.

## Production promotion

Production launch detection, the original LiveTray identity, and the ordinary
popup-preview lifecycle test identity are restored. ProviderFactory defaults to
production: refresh and same-directory encrypted-cache migration are enabled.
The old main must stop before the new production binary performs authenticated
requests. Installation retains the existing Downloads executable path and startup
entry. Credentials are never included in release archives. The old executable
remains a rollback artifact, but cannot read the new encrypted widget cache.

## Installed release verification

- All 175 assertions pass, including the new production-default regression check.
- Production build: zero warnings/errors; graceful preview lifecycle test passes.
- Installed path: `C:/Users/Administrator/Downloads/CodexBarWin.exe`.
- SHA-256: `0F07026DD4CCB7F2225B7201337A130C37E2BA5AA11B22D56980F55577735232`.
- The old process exited gracefully before replacement and authenticated checks.
- Installed API check at 22:45 IST returned status OK and source Live for both:
  Codex weekly 50%; Claude session 26%, weekly 22%; no warnings or re-login.
- A subsequent installed-process popup capture at 22:46 also returned current data
  and preserved the approved black/orange compact UI.
- Claude CLI login file hash is unchanged. Production DPAPI cache exists;
  no plaintext widget cache exists. No account secrets are in release artifacts.
- The production app is running. A duplicate launch exited, leaving one instance.
- The HKCU Run entry remains the quoted installed path, with no StartupApproved
  disable override. No Windows restart was performed during verification.
- Version 1.1.0 executable and pre-upgrade source/bundle copies are preserved in
  `outputs/archive/before-provider-fixes-1.2.0/` under the task workspace.

Automatic OAuth refresh is enabled in production. Expiry/401 recovery remains
covered by fake-client tests; real token rotation was not forced unnecessarily.
