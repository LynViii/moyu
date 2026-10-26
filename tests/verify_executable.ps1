param([switch]$CaptureWindow)
$ErrorActionPreference = 'Stop'
Add-Type 'using System; using System.Runtime.InteropServices; public class ExecutableDpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr w,uint m,IntPtr p,IntPtr l); [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr w); }'
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
$exitProcess = $null
try {
    $first = Start-Process -FilePath $copy -ArgumentList '--silent' -WorkingDirectory $portable -PassThru
    if (!$first.WaitForInputIdle(10000)) { throw 'Silent startup did not initialize' }
    Start-Sleep -Milliseconds 500
    $first.Refresh()
    if ($first.HasExited -or $first.MainWindowHandle -ne 0) { throw 'Silent startup showed a window or exited' }
    Write-Output 'PASS: silent startup stays alive without a visible main window'
    $second = Start-Process -FilePath $copy -WorkingDirectory $portable -PassThru
    if (!$second.WaitForExit(10000) -or $second.ExitCode -ne 0) { throw 'Second launch failed' }
    $wait = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Milliseconds 100
        $first.Refresh()
        if ($first.HasExited) { throw "Application exited on startup: $($first.ExitCode)" }
    } while ($first.MainWindowHandle -eq 0 -and $wait.ElapsedMilliseconds -lt 10000)
    if ($first.MainWindowHandle -eq 0) { throw 'No startup window' }
    if ($first.MainWindowTitle -ne $name) { throw 'Unexpected application title' }
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
    $mainHandle=$first.MainWindowHandle
    [ExecutableDpi]::PostMessage($mainHandle,0x0112,[IntPtr]0xF060,[IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 300
    $first.Refresh()
    if ($first.HasExited -or [ExecutableDpi]::IsWindowVisible($mainHandle)) { throw ('Closing window did not hide to tray; exited='+$first.HasExited) }
    $exitProcess = Start-Process -FilePath $copy -ArgumentList '--exit' -WorkingDirectory $portable -PassThru
    if (!$exitProcess.WaitForExit(10000) -or !$first.WaitForExit(10000)) { throw 'Explicit tray-process exit failed' }
    if ($first.ExitCode -ne 0 -or $exitProcess.ExitCode -ne 0) { throw 'Exit was not clean' }
    Write-Output 'PASS: real EXE close-to-tray and explicit clean exit'
} finally {
    foreach ($p in @($exitProcess,$second,$first)) {
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
