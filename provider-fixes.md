# Provider security and freshness fixes

Branch: `fix/provider-security-and-freshness`, based on main `8d67a58`.
Not merged or installed. Verified on Windows, 17 October 2026.

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

## Side-by-side safety

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

## Reproduce

```powershell
./scripts/Test-Widget.ps1 -Output test-results-provider-fixes
dotnet publish source/Tray/CodexBarWin.csproj -c Release -o publish-provider-fixes
./scripts/Test-Lifecycle.ps1 -Executable ./publish-provider-fixes/CodexBarWin.exe
./publish-provider-fixes/CodexBarWin.exe --status result.json
```

The preview executable can run beside main and uses the same approved widget UI.
Do not register this preview for startup.

## Before a future promotion

After approval, stop the old main before permitting real token rotation. Restore
normal Program preview detection and main mutex/event names, set ProviderFactory's
default preview to false, and make the lifecycle smoke test target the ordinary
popup-preview identity. Production Claude construction then enables refresh and
same-directory protected-cache migration. Re-run tests, publish, and install using
the existing startup path. Do not copy either account's credentials into release
archives. Keep an executable rollback, but remember the old main cannot read the
new encrypted widget cache after production migration.
