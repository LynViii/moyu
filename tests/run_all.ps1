$ErrorActionPreference = 'Stop'
foreach ($test in @('verify_modes.ps1', 'verify_scenes.ps1', 'verify_executable.ps1')) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $test)
    if ($LASTEXITCODE -ne 0) { throw "Failed: $test" }
}
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify_scenes.ps1') -DpiUnaware
if ($LASTEXITCODE -ne 0) { throw 'Failed: logical 96 DPI scene checks' }
Write-Output 'ALL TESTS PASSED'
