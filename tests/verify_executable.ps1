param([switch]$CaptureWindow)
$ErrorActionPreference = 'Stop'
Add-Type 'using System.Runtime.InteropServices; public class ExecutableDpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
[ExecutableDpi]::SetProcessDPIAware() | Out-Null
if ($CaptureWindow) {
    Add-Type -AssemblyName System.Drawing
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowCapture {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
}
'@
}
$root = Split-Path $PSScriptRoot -Parent
$name = ([string][char]0x6478) + [char]0x9c7c
if (Get-Process -Name $name -ErrorAction SilentlyContinue) { throw 'Close the running application before executable testing.' }
$exe = Join-Path $root ($name + '.exe')
$portable = Join-Path $root ('.build\portable-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $portable | Out-Null
$copy = Join-Path $portable ($name + '.exe')
Copy-Item -LiteralPath $exe -Destination $copy
$first = $null
$second = $null
try {
    $first = Start-Process -FilePath $copy -WorkingDirectory $portable -PassThru
    $wait = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Milliseconds 100
        $first.Refresh()
        if ($first.HasExited) { throw "Application exited on startup: $($first.ExitCode)" }
    } while ($first.MainWindowHandle -eq 0 -and $wait.ElapsedMilliseconds -lt 10000)
    if ($first.MainWindowHandle -eq 0) { throw 'No startup window' }
    if ($first.MainWindowTitle -ne $name) { throw 'Unexpected application title' }
    $second = Start-Process -FilePath $copy -WorkingDirectory $portable -PassThru
    if (!$second.WaitForExit(10000)) { throw 'Second instance did not hand off' }
    if ($second.ExitCode -ne 0) { throw 'Second launch failed' }
    $first.Refresh()
    if ($first.HasExited -or !$first.Responding) { throw 'First instance not responding' }
    if ($CaptureWindow) {
        Start-Sleep -Milliseconds 500
        $rect = New-Object WindowCapture+Rect
        if (![WindowCapture]::GetWindowRect($first.MainWindowHandle, [ref]$rect)) { throw 'Window rectangle unavailable' }
        $bitmap = New-Object Drawing.Bitmap ($rect.Right-$rect.Left),($rect.Bottom-$rect.Top)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $hdc = $graphics.GetHdc()
        try {
            if (![WindowCapture]::PrintWindow($first.MainWindowHandle, $hdc, 2)) { throw 'PrintWindow failed' }
        } finally { $graphics.ReleaseHdc($hdc); $graphics.Dispose() }
        try {
            $snapshot = Join-Path $env:TEMP ('moyu-window-' + [Guid]::NewGuid().ToString('N') + '.png')
            $bitmap.Save($snapshot)
            Write-Output $snapshot
        } finally { $bitmap.Dispose() }
    }
    Write-Output ('PASS: portable EXE startup, title, single-instance handoff; version=' + [Diagnostics.FileVersionInfo]::GetVersionInfo($copy).ProductVersion)
    if (@(Get-ChildItem -LiteralPath $portable).Count -ne 1) { throw 'Unexpected required sidecar or generated file' }
} finally {
    foreach ($p in @($second,$first)) {
        if ($null -ne $p) {
            $p.Refresh()
            if (!$p.HasExited) {
                $p.CloseMainWindow() | Out-Null
                if (!$p.WaitForExit(5000)) { $p.Kill(); $p.WaitForExit() }
            }
            $p.Dispose()
        }
    }
}
