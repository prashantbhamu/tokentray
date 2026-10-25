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
credentials are bundled. Source and design history are retained in this private
repository; obsolete preview binaries and local test outputs are not published.

SHA-256 of **TokenTray.exe**:

```text
0F07026DD4CCB7F2225B7201337A130C37E2BA5AA11B22D56980F55577735232
```
