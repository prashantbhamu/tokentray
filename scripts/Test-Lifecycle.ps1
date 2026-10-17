param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $Executable).Path
$startInfo = [System.Diagnostics.ProcessStartInfo]::new($resolved)
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.ArgumentList.Add('--popup-preview')
$startInfo.Environment['CODEXBARWIN_NO_NETWORK'] = '1'
$preview = [System.Diagnostics.Process]::Start($startInfo)
try {
    # The initial native application startup is fast, but wait for its named event,
    # not an arbitrary sleep, so Quit is tested while refresh may still be running.
    $quit = $null
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    while (-not $quit -and $watch.Elapsed.TotalSeconds -lt 10) {
        try { $quit = [System.Threading.EventWaitHandle]::OpenExisting('Local\CodexBarWin.CircularPopupPreview.Quit') }
        catch [System.Threading.WaitHandleCannotBeOpenedException] { [System.Threading.Thread]::Sleep(50) }
    }
    if (-not $quit) { throw 'Preview did not initialize its shutdown signal.' }
    try { $quit.Set() | Out-Null } finally { $quit.Dispose() }
    if (-not $preview.WaitForExit(10000)) { throw 'Graceful Quit timed out.' }
    if ($preview.ExitCode -ne 0) { throw "Preview failed with exit code $($preview.ExitCode)." }
    'PASS: graceful preview shutdown, with network disabled and refresh potentially in flight.'
} finally {
    # Only the exact process this test created is eligible for cleanup.
    if (-not $preview.HasExited) { $preview.Kill(); $preview.WaitForExit() }
    $preview.Dispose()
}
