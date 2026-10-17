# CodexBarWin

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

87 checks cover weekly selection, session exclusion, missing values, clamping,
native image sizes, transparency, meter orientation, and full provider-colored outlines.
