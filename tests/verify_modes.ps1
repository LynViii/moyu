$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type 'using System.Runtime.InteropServices; public class DpiCheck { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
[DpiCheck]::SetProcessDPIAware() | Out-Null
[Windows.Forms.Application]::EnableVisualStyles()
$exe = Join-Path $PSScriptRoot ('..\' + [char]0x6478 + [char]0x9c7c + '.exe')
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $exe))
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$introType = $assembly.GetType('WindowsFish.IntroForm')
$settingsPath = Join-Path $PSScriptRoot ('settings-test-' + [Guid]::NewGuid().ToString('N') + '.xml')
$constructor = $introType.GetConstructor($flags, $null, [type[]]@([string]), $null)
$form = $constructor.Invoke([object[]]@($settingsPath.PSObject.BaseObject))
$type = $assembly.GetType('WindowsFish.FishMode')
$modes = $assembly.GetType('WindowsFish.UpdateScreenMode').GetMethod('All').Invoke($null, @())
try {
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    if ($modes.Length -ne 3) { throw 'Expected three modes' }
    $modeButtons = @($form.Controls | Where-Object { $_.Tag -ne $null })
    $modeButtons[1].PerformClick()
    $introType.GetField('returnAfter', $flags).GetValue($form).SelectedIndex = 2
    $introType.GetField('blackoutSecondary', $flags).GetValue($form).Checked = $false
    $restored = $constructor.Invoke([object[]]@($settingsPath.PSObject.BaseObject))
    try {
        if ($introType.GetField('selectedMode', $flags).GetValue($restored).Id -ne 'restart') { throw 'Mode not restored' }
        if ($introType.GetField('returnAfter', $flags).GetValue($restored).SelectedIndex -ne 2) { throw 'Duration not restored' }
        if ($introType.GetField('blackoutSecondary', $flags).GetValue($restored).Checked) { throw 'Monitor option not restored' }
    } finally { $restored.Dispose() }
    Write-Output 'PASS: UI changes saved and restored in a fresh intro form'
    $arguments = [string[]]@('--mode', 'prepare', '--minutes', '25', '--speed', 'slow', '--primary-only')
    $introType.GetMethod('ApplyArguments', $flags).Invoke($form, [object[]]@(,$arguments)) | Out-Null
    $reload = $constructor.Invoke([object[]]@($settingsPath.PSObject.BaseObject))
    try {
        if ($introType.GetField('returnAfter', $flags).GetValue($reload).SelectedIndex -ne 4) { throw 'Custom preset not restored' }
        if ($introType.GetField('customMinutes', $flags).GetValue($reload).Value -ne 25) { throw 'Custom minutes not restored' }
        if ($introType.GetField('speedChoice', $flags).GetValue($reload).SelectedIndex -ne 0) { throw 'Speed not restored' }
        if ($introType.GetField('selectedMode', $flags).GetValue($reload).Id -ne 'prepare') { throw 'CLI mode not applied' }
    } finally { $reload.Dispose() }
    $introType.GetField('delayStart', $flags).GetValue($form).Checked = $true
    $start = $introType.GetField('startButton', $flags).GetValue($form)
    $start.PerformClick()
    if ($null -eq $introType.GetField('launchTimer', $flags).GetValue($form)) { throw 'Delay missing' }
    $start.PerformClick()
    if ($null -ne $introType.GetField('launchTimer', $flags).GetValue($form)) { throw 'Cancel failed' }
    $start.PerformClick()
    $delayWait = [Diagnostics.Stopwatch]::StartNew()
    while ($delayWait.ElapsedMilliseconds -lt 5400) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 10 }
    $launched = $introType.GetField('fishMode', $flags).GetValue($form)
    if ($null -eq $launched) { throw 'Delayed launch failed' }
    $type.GetMethod('Stop').Invoke($launched, @()) | Out-Null
    if ([string]::IsNullOrEmpty($introType.GetField('sessionSummary', $flags).GetValue($form).Text)) { throw 'Summary missing' }
    Write-Output 'PASS: CLI, custom duration and speed persistence, delayed launch/cancel, elapsed summary'
    $previewCloser = New-Object Windows.Forms.Timer
    $previewCloser.Interval = 400
    $previewCloser.Add_Tick({
        $p = $introType.GetField('previewWindow', $flags).GetValue($form)
        if ($null -ne $p) { $p.Close() }
        $previewCloser.Stop()
    })
    try {
        $previewCloser.Start()
        $introType.GetMethod('ShowPreview', $flags).Invoke($form, @()) | Out-Null
        if ($null -ne $introType.GetField('previewWindow', $flags).GetValue($form)) { throw 'Preview retained' }
        if ($null -ne $introType.GetField('fishMode', $flags).GetValue($form)) { throw 'Preview started fullscreen' }
        Write-Output 'PASS: modal preview opens and closes without starting fullscreen'
    } finally { $previewCloser.Dispose() }
    $onlyPrimary = [Activator]::CreateInstance($type, [object[]]@($form, $modes[0], 0, $false))
    try {
        $type.GetMethod('Start').Invoke($onlyPrimary, @()) | Out-Null
        $primaryWindows = $type.GetField('windows', $flags).GetValue($onlyPrimary)
        if ($primaryWindows.Count -ne 1 -or $primaryWindows[0].GetType().Name -ne 'UpdateForm') { throw 'Primary-only selection failed' }
        Write-Output 'PASS: secondary coverage disabled creates only the primary window'
    } finally { $type.GetMethod('Stop').Invoke($onlyPrimary, @()) | Out-Null }
    $bitmap = New-Object Drawing.Bitmap $form.Width,$form.Height
    $form.DrawToBitmap($bitmap, (New-Object Drawing.Rectangle 0,0,$form.Width,$form.Height))
    $bitmap.Save((Join-Path $PSScriptRoot 'intro-compact.png'))
    $bitmap.Dispose()
    foreach ($minutes in @(0, 15)) {
        $session = [Activator]::CreateInstance($type, [object[]]@($form, $modes[0], $minutes))
        try {
            $form.Hide()
            $type.GetMethod('Start').Invoke($session, @()) | Out-Null
            [Windows.Forms.Application]::DoEvents()
            $windows = $type.GetField('windows', $flags).GetValue($session)
            if ($windows.Count -ne [Windows.Forms.Screen]::AllScreens.Length) { throw 'Display coverage mismatch' }
            $oldWindows = @($windows.ToArray())
            $clock = $type.GetField('elapsed', $flags).GetValue($session)
            $type.GetMethod('DisplaySettingsChanged', $flags).Invoke($session, [object[]]@($null, [EventArgs]::Empty)) | Out-Null
            $wait = [Diagnostics.Stopwatch]::StartNew()
            while ($wait.ElapsedMilliseconds -lt 700) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 10 }
            if (@($oldWindows | Where-Object { !$_.IsDisposed }).Count -ne 0) { throw 'Old display windows leaked' }
            if ($form.Visible) { throw 'Display rebuild returned to intro' }
            if (![object]::ReferenceEquals($clock, $type.GetField('elapsed', $flags).GetValue($session))) { throw 'Display rebuild reset duration' }
            if ($windows.Count -ne [Windows.Forms.Screen]::AllScreens.Length) { throw 'Rebuilt display count mismatch' }
            if (@($windows | Where-Object { $_.GetType().Name -eq 'UpdateForm' }).Count -ne 1) { throw 'Expected exactly one primary status window' }
            foreach ($window in $windows) {
                if ($window.Bounds -ne [Windows.Forms.Screen]::FromControl($window).Bounds) { throw 'Monitor bounds mismatch' }
            }
            Write-Output 'PASS: display event rebuilt windows without resetting session or returning to intro'
            $primary = @($windows | Where-Object { $_.GetType().Name -eq 'UpdateForm' })[0]
            $primary.WindowState = [Windows.Forms.FormWindowState]::Minimized
            $type.GetMethod('ActivatePrimary').Invoke($session, @()) | Out-Null
            [Windows.Forms.Application]::DoEvents()
            if ($primary.WindowState -ne [Windows.Forms.FormWindowState]::Normal) { throw 'Fullscreen restore failed' }
            $region = $primary.GetType().GetField('spinnerBounds', $flags).GetValue($primary)
            if ($region.IsEmpty -or ($region.Width * $region.Height) -gt ($primary.Width * $primary.Height / 10)) { throw 'Animation refresh region is not local' }
            $form.Location = New-Object Drawing.Point -20000,-20000
            $windows[0].Close()
            if ($windows[0].IsDisposed) { throw 'Ordinary close was not blocked' }
            $check = $type.GetMethod('CheckAutoReturn', $flags)
            $check.Invoke($session, [object[]]@(14.99)) | Out-Null
            if ($windows.Count -eq 0) { throw 'Returned before deadline' }
            $check.Invoke($session, [object[]]@(15.0)) | Out-Null
            if ($minutes -eq 0) {
                if ($windows.Count -eq 0) { throw 'Unlimited mode timed out' }
                $event = New-Object Windows.Forms.KeyEventArgs ([Windows.Forms.Keys]::Escape)
                $type.GetMethod('FullscreenKeyDown', $flags).Invoke($session, [object[]]@($windows[0], $event.PSObject.BaseObject)) | Out-Null
            }
            if ($windows.Count -ne 0 -or !$form.Visible) { throw 'Return cleanup failed' }
            if (![Windows.Forms.Screen]::FromControl($form).WorkingArea.IntersectsWith($form.Bounds)) { throw 'Intro returned offscreen' }
            if ($null -ne $type.GetField('returnTimer', $flags).GetValue($session)) { throw 'Timer leaked' }
            if ($null -ne $type.GetField('displayTimer', $flags).GetValue($session)) { throw 'Display timer leaked' }
            if ($type.GetField('displaySubscribed', $flags).GetValue($session)) { throw 'Display event subscription leaked' }
            Write-Output "PASS: minutes=$minutes, ordinary close blocked, return and cleanup verified"
            Write-Output 'PASS: fullscreen restored, animation redraw restricted, offscreen intro recovered'
        } finally {
            $type.GetMethod('Stop').Invoke($session, @()) | Out-Null
        }
    }
    Write-Output ('PASS: intro DPI/layout rendered, modes=' + $modes.Length)
} finally {
    $form.Close()
    $form.Dispose()
}
