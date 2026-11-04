# TokenTray v1.3.1

- The popup and tray no longer keep showing the last Claude reading after you sign out, after
  your login expires or is rejected, or after switching accounts. A cached reading is now shown
  only for a temporary failure (network, server, rate limit) of the same account, and is
  labelled with its age.

# TokenTray v1.3.0

- Claude logins are now read-only: TokenTray no longer refreshes Claude OAuth tokens, which
  could rotate the refresh token and sign Claude Code out. An expired login is reported until
  Claude Code refreshes it.
- Claude usage always follows Claude Code's current login, so an account switch or sign-out is
  reflected immediately instead of a previously cached account winning. No login copy is kept.
- A malformed Claude credentials file (for example `[]`) is treated as "no login" instead of
  causing an error.
- The bundled .NET runtime is pinned to 8.0.33, which fixes published runtime advisories
  affecting 8.0.28 and earlier.
- Codex: fractional usage percentages are shown correctly; a missing reset time no longer shows
  as "resets in now"; an expired Codex login is reported alongside the local fallback.
- Codex session fallback reads log files from the end in linear time, even for multi-megabyte
  single records, instead of scanning them in full.
- The executable, window titles and tray text are now named TokenTray. Settings and startup
  locations are unchanged.

# TokenTray v1.2.0

Portable Windows x64 usage widget for Codex and Claude Code.

- Live usage in a compact theme-aware popup.
- Black Codex and orange Claude outlined tray meters.
- Windows startup option, single instance, and no extra taskbar button.
- Windows-user-encrypted Claude widget cache with safe legacy migration.
- Explicit age and provenance for local or cached Codex usage.
- Automatic token refresh and 175 passing regression checks.

Download **TokenTray.exe**, put it in a permanent folder, and run it. Sign in
to Codex/Claude on that PC, then enable **Launch at login** in tray Settings.
No installer or separate .NET runtime is required. This is an unsigned portable
application, not a signed installer. Automatic update downloads are not implemented.

The first TokenTray release distributes the already-tested CodexBarWin v1.2.0
binary under a new filename. Legacy UI labels, internal paths, and startup
registration remain unchanged for existing-installation compatibility. No account
credentials are bundled. Source and design history are retained in this
repository; obsolete preview binaries and local test outputs are not published.

SHA-256 of **TokenTray.exe**:

```text
0F07026DD4CCB7F2225B7201337A130C37E2BA5AA11B22D56980F55577735232
```
