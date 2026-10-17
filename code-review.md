# Code review and cleanup — 17 October 2026

## Scope and conclusion

Reviewed the native tray/popup application, settings, startup registration, lifecycle, diagnostics,
renderers, test/build configuration, and the recovered source of the original provider library.
This is a local source/static review plus regression testing, not a claim of exhaustive security
auditing or independent verification of third-party authentication APIs.

The UI/lifecycle issues below are fixed. The approved icon, provider colors, compact circular popup,
fully opaque Windows-theme background, live refresh, and startup behavior are preserved. No P0 issue
was found in this review. Two inherited provider-library risks remain explicitly documented below.

## Fixed findings

### P1 — Settings controls clipped at 125% display scale

The settings dialog used fixed coordinates and heights with point-sized fonts. At 125%, its endpoint
label and Cancel button visibly clipped. Replaced fixed geometry with DPI-aware auto-sized layout;
added preferred-size assertions and inspected 100%/125% native screenshots.

### P2 — Refresh continuation could update disposed controls after Quit

In-flight work previously continued into the popup/tray after disposal. The UI refresh path now guards
exit state, ignores superseded settings generations, catches refresh failures, and releases timers,
icons, native handles and context menus exactly once. A graceful-shutdown smoke test passes.
Underlying provider HTTP calls are synchronous and cannot be cancelled, but their late result cannot
repaint the closed widget. Their worker threads do not keep the process alive after Quit.

### P2 — A refresh could apply results from an obsolete provider/network configuration

Settings changes now invalidate the previous refresh generation and clear cached state. A late result
is discarded and a refresh is scheduled using the new configuration.

### P2 — Settings saves could silently fail or truncate the file

Saving now writes a uniquely named same-directory temporary file and atomically replaces settings.
Write failures are surfaced, temporary files are cleaned up, and a failed settings save attempts to
restore the previous startup setting. Invalid files load safe defaults without deleting the input.

### P2 — Error-state resizing could run into the taskbar

Placement is now computed against the selected monitor's working area and reapplied when visible
content changes height. Long provider errors are bounded; long plan text cannot produce a negative
text rectangle. Geometry tests include Full HD, taller error states and negative monitor coordinates.

### P2 — A stale startup entry could look enabled for the wrong executable

Startup detection now matches the actual quoted or unquoted executable path. Writes return the
verified current state. The installed current-user Run entry is preserved; no duplicate shortcut is created.

### P2 — Diagnostics bypassed the live provider/network policy

Provider construction is centralized. Live mode and diagnostics now honor provider enable flags,
per-provider network flags, and the documented global no-network environment override.

### P2 — Cached and invalid usage could look like fresh or zero usage

Cached transient-error results are labeled with their original fetch time and a `cached` tooltip suffix.
No-data resets clear the cache. Invalid/non-finite popup percentages display a dash, not a fabricated 0%.
The tray continues to use weekly data only, never Claude's session percentage as a substitute.

### P3 — Experimental and recovered-build residue

Removed the obsolete icon-preview form, old winner-number logic, unused glass/gradient tokens and
misleading recovered assembly metadata. Added clear 1.1.0 SDK-generated metadata, consistent formatting,
warnings-as-errors and reproducible verification scripts. Renamed RingTests to WidgetTests. Extracted
diagnostics, provider construction, popup placement and cached-usage behavior into focused helpers.

`--open` now signals an existing instance rather than requiring a restart, and `--quit` provides a
graceful local shutdown path. Normal Windows startup still leaves the popup closed.

## Inherited provider-library risks — not changed in this cleanup

The provider component is the original `bundle/CodexBarWin.Core.dll`, SHA-256:

`1DC76AE258A8FAEB6A446D00957B257FB933A6510F67542CFF993A331E3A7303`

Its recovered source was inspected for networking, parsing, local file reads, logging, cooldowns,
credential refresh and persistence. The current binary was not rebuilt or replaced, to avoid silently
changing sign-in/token behavior as part of UI/code cleanup. Source is recovered/decompiled, not a
reproducible upstream build provenance guarantee.

### P2 — Refreshed Claude credentials may be cached as plaintext JSON

Recovered `ClaudeCredentialStore.WriteToStore` serializes access/refresh tokens into a local profile
JSON file. It has no application-level encryption. This is an at-rest risk, mitigated only by existing
profile/filesystem permissions or disk protection; those protections were not audited or changed.
Recommended separate change: source-build the provider component and implement a compatible protected
credential store with explicit migration/rollback tests. No credential files were inspected, copied,
printed, deleted, or migrated for this review.

### P2 — Codex local fallback can be stale while appearing freshly fetched

Recovered `CodexProvider.BuildFromSession` constructs an OK snapshot with the current fetch time even
when its session record is old, and the session fallback does not apply the log fallback's age warning.
If the network endpoint fails, fallback data can therefore look fresher than it is. The new UI cache
correctly labels errors it receives, but cannot infer age omitted by the provider binary.
Recommended separate provider change: carry source timestamps and data provenance through the model,
then test offline/idle/reset cases. Normal live endpoint fetches remain unchanged.

### P3 — Parser hardening and platform coverage

Some original parsers coerce absent or unexpected numeric fields to zero and are tied to existing API
shapes. Authentication/network tests do not cover every upstream response schema. A source-built provider
test suite should add malformed/numeric-boundary cases. PerMonitorV2/live cross-monitor DPI migration
is also outside the certified display matrix; see `verification.md`.

## Verification and cleanup

- 133 assertions pass at actual 1920 x 1080, 100% and 125% Windows display scale.
- Build has zero warnings/errors; formatting check passes.
- Graceful quit smoke test passes; duplicate-instance and open behavior are checked during final installation.
- Dependency scan including transitives reports no known vulnerable packages from the configured source.
- Original 150% scale restored; no separate text-size test applied, per user request.
- All five merged design branch refs are removed only after archiving a full Git bundle.
- Superseded preview executables, rollback copies, old source bundles and loose screenshots are moved to
  an explicit task-local archive, not permanently deleted. The known-working pre-cleanup executable remains available.
- Final source and binary are retained as the current release; startup and live usage are verified on installation.
