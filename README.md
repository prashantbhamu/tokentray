# CodexBarWin

## Circular usage popup

The main app implements the supplied circular-meter reference as a native WinForms popup.
Black Codex and orange Claude rings, a single divider, percentage centers, and an icon-only
refresh action replace the previous cards and horizontal meters. Fully opaque,
Windows-theme-aware, 284px wide after trimming unused right-side space. Escape dismisses; F5 or the refresh icon refreshes.

`--popup-preview` opens a live preview popup with a separate single-instance guard
and a temporary tray icon whose tooltip starts with PREVIEW. Settings changes are disabled
in preview mode. It never replaces the installed executable or changes Windows startup.
Right-click its tray icon and choose Close branch preview to exit.

`--shot PATH --sample-data --preview-light` captures the reference sample state;
`--preview-dark` captures the dark variant. Tests now include popup refresh, loading,
error, theme, sizing and provider-color checks (99 checks total).

Normal startup runs in the tray without opening the popup. `--open` opens it after live usage loads.

Baseline: current compact, opaque popup following the Windows app theme.
Recovered tray sources reference the unchanged installed provider core assembly.

Build with .NET 8 SDK:

    dotnet publish source/Tray/CodexBarWin.csproj -c Release -o publish

The main executable starts directly in the live tray. A per-session mutex prevents duplicate instances.

## Outlined weekly bars

Left vertical meter: Codex weekly usage, black. Right meter: Claude weekly usage, orange.
Both meters fill from the bottom. The icon has no background tile: the unused
space is transparent. There are no numerals. Each full bar, including its gray unused
area, has a thin outline in its provider's color. Missing weekly data is not replaced with session usage.

`--icon-preview` opens an isolated visual test window. Each sample has
a button to try a temporary test icon in the tray. Closing the preview window
removes that test icon. No installed executable or startup entry is changed.
`--tray-preview` starts the live tray and opens its popup. Normal startup keeps the popup closed.

Checks:

    dotnet run --project tests/RingTests.csproj -c Release -- test-results

Popup usage fills use the same solid provider colors as the tray. The tooltip displays
both percentages without a Weekly prefix; values still represent weekly usage.

99 checks cover weekly selection, session exclusion, missing values, clamping,
native image sizes, transparency, tray outlines, circular popup, refresh, loading/error, and light/dark themes.
