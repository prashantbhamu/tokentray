# Display and regression verification — 17 October 2026

## Actual Windows display tests

These were real Windows Scale changes through Settings, not image resizing or a simulated viewport.
The physical display stayed 1920 x 1080 at 60 Hz. Text-size experimentation was cancelled at the
user's request; its pending unapplied slider change was reset to 100% without applying anything.

| Windows scale | Observed app DPI | Screen bounds | Working area | Native tray size | Final tests |
| --- | --- | --- | --- | --- | --- |
| 100% | 96 | 1920 x 1080 | 1920 x 1032 | 16 x 16 | 133 passed |
| 125% | 120 | 1920 x 1080 | 1920 x 1020 | 20 x 20 | 133 passed |
| Original 150%, restored | 144 | 1920 x 1080 | 1920 x 1008 | 24 x 24 | Regression run also performed |

The popup's sample-state screenshot is 284 x 293 at both requested settings. Its fixed pixel
dimensions are intentional to preserve the approved compact UI. Percentage labels, reset text,
refresh control and footer were visually inspected at actual native render sizes, without clipping.

## Issue found and fixed during testing

The old fixed-position settings dialog clipped an endpoint label and Cancel button at 125%.
It now uses DPI-aware, content-sized table/flow layouts and auto-sized controls. Updated screenshots
at both scales show all labels and buttons completely. Tests also compare each control's allocated
size with its preferred text size at the active DPI.

## Other checks

- Release build: zero warnings and errors; warnings treated as errors.
- Source formatting: `dotnet format whitespace --verify-no-changes`.
- 133 regression assertions at both requested settings.
- Graceful preview shutdown smoke test while refresh may still be running.
- NuGet vulnerability scan including transitive packages: no reported vulnerable packages in the configured source.
- Popup bounds tested for Full HD work areas, taller error states, and negative-coordinate secondary monitors.
- Light/dark native render checks and 0%/100% usage render checks.
- Network-disabled test path performs no network fetches.

## Evidence

Release evidence is delivered in `outputs/verification/`: `fullhd-100.png`, `fullhd-125.png`,
`settings-100.png`, `settings-125.png`, and 16px/20px tray renders. The final release and source
are delivered separately in `outputs/`. Test renderings use sample data; live provider status is
checked separately during installation and is not hard-coded into the widget.

## Limits

These tests cover clean launches under the selected scales. The process remains SystemAware,
not PerMonitorV2: automatic DPI migration between differently scaled monitors without restarting
has not been certified. Secondary-monitor placement has geometry tests, not a physical multi-monitor test.
No boot/sign-out was forced; automatic startup is verified through the current-user Run entry.
