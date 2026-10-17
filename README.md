# CodexBarWin local branch tests

Baseline: current compact, opaque popup following the Windows app theme.
Recovered tray sources reference the unchanged installed provider core assembly.

Build with .NET 8 SDK:

    dotnet publish source/Tray/CodexBarWin.csproj -c Release -o publish

Branch builds must not overwrite Downloads/CodexBarWin.exe or change startup.

## design/side-meters

Left vertical meter: Codex weekly usage, black. Right meter: Claude weekly usage, orange.
Both meters fill from the bottom. The icon has no background tile: the unused
space is transparent, and only the numeral and meters are drawn.
The center number is the higher weekly usage, in that provider's color.
Ties prefer Codex. Missing weekly data is not replaced with session usage.

The branch executable opens a visual test window by default. Each sample has
a button to try a temporary test icon in the tray. Closing the preview window
removes that test icon. No installed executable or startup entry is changed.
`--tray-preview` explicitly starts the branch build with live provider usage.

Checks:

    dotnet run --project tests/RingTests.csproj -c Release -- test-results

62 checks cover weekly selection, session exclusion, winner colors, missing
values, ties, clamping, exact native image sizes, transparent empty space, and meter orientation.
