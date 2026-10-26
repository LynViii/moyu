$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type 'using System.Runtime.InteropServices; public class DpiCheck { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
[DpiCheck]::SetProcessDPIAware() | Out-Null
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')))
$flags=[Reflection.BindingFlags]'Instance,NonPublic,Public'
$introType=$assembly.GetType('WindowsFish.IntroForm')
$type=$assembly.GetType('WindowsFish.FishMode')
$pType=$assembly.GetType('WindowsFish.Preferences')
$path=Join-Path $PSScriptRoot ('settings-test-'+[Guid]::NewGuid().ToString('N')+'.xml')
$constructor=$introType.GetConstructor($flags,$null,[type[]]@([string]),$null)
$form=$constructor.Invoke([object[]]@($path.PSObject.BaseObject))
function Get($o,$n) { return ,$o.GetType().GetField($n,$flags).GetValue($o) }
function Call($o,$n,$values) {
    for($j=0;$j -lt $values.Length;$j++){if($null -ne $values[$j]){$values[$j]=$values[$j].PSObject.BaseObject}}
    return $o.GetType().GetMethod($n,$flags).Invoke($o.PSObject.BaseObject,$values)
}
function Pump($milliseconds) {
    $w=[Diagnostics.Stopwatch]::StartNew()
    while($w.ElapsedMilliseconds -lt $milliseconds){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 5}
}
$modes=$assembly.GetType('WindowsFish.UpdateScreenMode').GetMethod('All').Invoke($null,@())
try {
    $form.Show()
    $buttons=@($introType.GetProperty('ContentControls',$flags).GetValue($form,$null)|Where-Object{$_.Tag -ne $null})
    $buttons[1].PerformClick()
    (Get $form delaySeconds).Value=1
    (Get $form blackoutSecondary).Checked=$false
    $restored=$constructor.Invoke([object[]]@($path.PSObject.BaseObject))
    try {
        if((Get $restored selectedMode).Id -ne 'restart' -or (Get $restored delaySeconds).Value -ne 1 -or (Get $restored blackoutSecondary).Checked){throw 'Preferences not restored'}
    } finally {$restored.Dispose()}
    $args=[string[]]@('--mode','prepare','--minutes','1','--speed','slow','--primary-only')
    Call $form ApplyArguments ([object[]]@(,$args)) | Out-Null
    if((Get $form selectedMode).Id -ne 'prepare' -or (Get $form speedChoice).SelectedIndex -ne 0){throw 'CLI state not applied'}
    [xml]$saved=Get-Content -LiteralPath $path -Raw -Encoding UTF8
    if($saved.preferences.HasAttribute('minutes')){throw 'Legacy auto-exit setting persisted'}
    Write-Output 'PASS: preferences, CLI compatibility and delay restore; legacy auto-exit removed'
    $start=Get $form startButton
    $start.PerformClick()
    if($null -eq (Get $form launchTimer)){throw 'Countdown missing'}
    $start.PerformClick()
    if($null -ne (Get $form launchTimer)){throw 'Countdown cancel failed'}
    $start.PerformClick()
    Pump 1600
    $session=Get $form fishMode
    if($null -eq $session){throw 'Delayed launch failed'}
    Call $session Stop @() | Out-Null
    Write-Output 'PASS: countdown starts, cancels, launches and cleans up'
    $closer=New-Object Windows.Forms.Timer
    $closer.Interval=350
    $closer.Add_Tick({$p=Get $form previewWindow;if($null -ne $p){$p.Close()};$closer.Stop()})
    try {
        $closer.Start()
        Call $form ShowPreview @() | Out-Null
        if($null -ne (Get $form previewWindow) -or $null -ne (Get $form fishMode)){throw 'Preview cleanup failed'}
    } finally {$closer.Dispose()}
    Write-Output 'PASS: modal preview opens and closes without starting fullscreen'
    $options=Get $form preferences
    $pType.GetField('DismissStartMenu',$flags).SetValue($options,$false)
    foreach($blackout in @($false,$true)) {
        $session=[Activator]::CreateInstance($type,[object[]]@($form,$modes[0],$blackout,1.0,$options))
        try {
            $form.Hide()
            Call $session Start @() | Out-Null
            Pump 150
            $windows=Get $session windows
            $expected=if($blackout){[Windows.Forms.Screen]::AllScreens.Length}else{1}
            if($windows.Count -ne $expected){throw 'Display coverage mismatch'}
            $old=@($windows.ToArray())
            $clock=Get $session elapsed
            Call $session DisplaySettingsChanged ([object[]]@($null,[EventArgs]::Empty)) | Out-Null
            Pump 650
            if(@($old|Where-Object{!$_.IsDisposed}).Count -ne 0){throw 'Old windows leaked'}
            if(![object]::ReferenceEquals($clock,(Get $session elapsed)) -or $form.Visible){throw 'Rebuild changed session state'}
            $primary=@($windows|Where-Object{$_.GetType().Name -eq 'UpdateForm'})[0]
            $primary.WindowState=[Windows.Forms.FormWindowState]::Minimized
            Call $session ActivatePrimary @() | Out-Null
            Pump 80
            if($primary.WindowState -ne [Windows.Forms.FormWindowState]::Normal){throw 'Restore failed'}
            $region=Get $primary spinnerBounds
            if($region.IsEmpty -or ($region.Width*$region.Height) -gt ($primary.Width*$primary.Height/10)){throw 'Animation redraw region too large'}
            $windows[0].Close()
            if($windows[0].IsDisposed){throw 'Fullscreen ordinary close not blocked'}
            if($null -ne $type.GetMethod('CheckAutoReturn',$flags)){throw 'Automatic return still exists'}
            $event=New-Object Windows.Forms.KeyEventArgs ([Windows.Forms.Keys]::Escape)
            Call $session FullscreenKeyDown ([object[]]@($windows[0],$event)) | Out-Null
            if($windows.Count -ne 0 -or !$form.Visible){throw 'Esc return failed'}
            if($null -ne (Get $session displayTimer) -or (Get $session displaySubscribed)){throw 'Display resources leaked'}
            Write-Output ('PASS: coverage='+$blackout+', display rebuild/restore, local redraw, close block and Esc cleanup')
        } finally {Call $session Stop @() | Out-Null}
    }
    $bitmap=New-Object Drawing.Bitmap $form.Width,$form.Height
    try {
        $form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle 0,0,$form.Width,$form.Height))
        $bitmap.Save((Join-Path $PSScriptRoot 'intro-compact.png'))
    } finally {$bitmap.Dispose()}
} finally {$form.Dispose()}
