param([string]$DotNet, [string]$Output = 'test-results')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $DotNet) {
    $runtime = Get-Command dotnet -ErrorAction SilentlyContinue
    $DotNet = if ($runtime) { $runtime.Source } else { Join-Path (Split-Path $repo -Parent) 'dotnet\dotnet.exe' }
}
if (-not (Test-Path -LiteralPath $DotNet)) { throw 'Install .NET 8 SDK or pass -DotNet with its full path.' }
Push-Location $repo
try {
    & $DotNet format whitespace 'source\Core\CodexBarWin.Core.csproj' --no-restore --verify-no-changes --verbosity quiet
    if ($LASTEXITCODE) { throw 'Provider source formatting check failed.' }
    & $DotNet format whitespace 'source\Tray\CodexBarWin.csproj' --no-restore --verify-no-changes --verbosity quiet
    if ($LASTEXITCODE) { throw 'Source formatting check failed.' }
    & $DotNet build 'source\Tray\CodexBarWin.csproj' -c Release --nologo
    if ($LASTEXITCODE) { throw 'Build failed.' }
    & $DotNet run --project 'tests\WidgetTests.csproj' -c Release -- $Output
    if ($LASTEXITCODE) { throw 'Widget tests failed.' }
} finally { Pop-Location }
