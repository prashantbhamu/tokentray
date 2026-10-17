# CodexBarWin local branch tests

Baseline: current compact, opaque popup following the Windows app theme.
Recovered tray sources reference the unchanged installed provider core assembly.

Build with .NET 8 SDK:

    dotnet publish source/Tray/CodexBarWin.csproj -c Release -o publish

Branch builds must not overwrite Downloads/CodexBarWin.exe or change startup.
