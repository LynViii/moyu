param([int]$FrameSeconds = 10)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type 'using System.Runtime.InteropServices; public class FixDpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
[FixDpi]::SetProcessDPIAware() | Out-Null
[Windows.Forms.Application]::EnableVisualStyles()
Add-Type -Path (Join-Path $PSScriptRoot 'FrameProbe.cs') -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Core
$root = Split-Path $PSScriptRoot -Parent
$a = [Reflection.Assembly]::LoadFrom((Join-Path $root (([string][char]0x6478) + [char]0x9c7c + '.exe')))
$f = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$pType = $a.GetType('WindowsFish.Preferences')
$iType = $a.GetType('WindowsFish.IntroForm')
$dType = $a.GetType('WindowsFish.SettingsDialog')
$uType = $a.GetType('WindowsFish.UpdateForm')
$mType = $a.GetType('WindowsFish.UpdateScreenMode')
$out = Join-Path $root '.build\acceptance'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$path = Join-Path $out ('settings-' + [Guid]::NewGuid().ToString('N') + '.xml')
function Get($o,$n) { return $o.GetType().GetField($n,$f).GetValue($o) }
function Put($o,$n,$v) { $o.GetType().GetField($n,$f).SetValue($o,$v.PSObject.BaseObject) }
function Call($o,$n,$values) {
    for ($j=0; $j -lt $values.Length; $j++) { if ($null -ne $values[$j]) { $values[$j]=$values[$j].PSObject.BaseObject } }
    return $o.GetType().GetMethod($n,$f).Invoke($o.PSObject.BaseObject,$values)
}
function Capture($form,$name) {
    $b=New-Object Drawing.Bitmap $form.Width,$form.Height
    try { $form.DrawToBitmap($b,(New-Object Drawing.Rectangle 0,0,$form.Width,$form.Height)); $b.Save((Join-Path $out ($name+'.png'))) } finally { $b.Dispose() }
}
$p = [Activator]::CreateInstance($pType,$true)
Put $p Palette 'light'
Put $p Monitor 'disconnected-screen-test'
$d=$dType.GetConstructor($f,$null,[type[]]@($pType),$null).Invoke([object[]]@($p))
try {
    $r=Call $d ReadOptions @()
    if ((Get $r Monitor) -ne 'disconnected-screen-test') { throw 'Offline display preference lost' }
    Call $d ResetDraft @() | Out-Null
    if ((Get (Call $d ReadOptions @()) Monitor) -ne '') { throw 'Reset must restore automatic display selection' }
    $d.Show()
    $area=New-Object Drawing.Rectangle 0,0,640,480
    Call $d FitToWorkArea ([object[]]@($area)) | Out-Null
    [Windows.Forms.Application]::DoEvents()
    if (!$area.Contains($d.Bounds)) { throw 'Settings outside work area' }
    $button=$d.AcceptButton
    $point=$d.PointToClient($button.PointToScreen([Drawing.Point]::Empty))
    $bounds=New-Object Drawing.Rectangle $point,$button.Size
    Capture $d 'settings-640x480'
    if (!$d.ClientRectangle.Contains($bounds)) { throw ('Settings Apply button clipped: ' + $bounds + '; client=' + $d.ClientRectangle) }
} finally { $d.Dispose() }
Write-Output 'PASS: offline monitor preserved; explicit reset restores automatic selection'
$form=$iType.GetConstructor($f,$null,[type[]]@([string]),$null).Invoke([object[]]@($path.PSObject.BaseObject))
try {
    $form.Show()
    Call $form ApplySettings ([object[]]@($p)) | Out-Null
    (Get $form win10).Checked=$true
    if ((Get (Get $form preferences) Palette) -ne 'light') { throw 'Scene switch overwrote explicit palette' }
    $sceneButton=Get $form win11
    foreach ($c in (Get $form content).Controls) {
        if ($c -is [Windows.Forms.Label] -and $c.Bounds.IntersectsWith($sceneButton.Bounds)) { throw 'Label overlaps scene preview' }
    }
    foreach ($size in @(@(1280,720),@(1024,768),@(800,600),@(640,480))) {
        $area=New-Object Drawing.Rectangle 0,0,$size[0],$size[1]
        Call $form FitToWorkArea ([object[]]@($area)) | Out-Null
        [Windows.Forms.Application]::DoEvents()
        if (!$area.Contains($form.Bounds)) { throw 'Window outside work area' }
        $button=Get $form startButton
        if (!$button.Parent.ClientRectangle.Contains($button.Bounds)) { throw 'Start button clipped' }
        if (!(Get $form content).AutoScroll) { throw 'Missing content scrolling' }
        Capture $form ('intro-'+$size[0]+'x'+$size[1])
    }
    Write-Output 'PASS: explicit palette retained; fixed action footer fits four work areas at current DPI'
} finally { $form.Dispose() }
# A directory at the destination forces a save failure without touching real user preferences.
$blocked=Join-Path $out ('blocked-'+[Guid]::NewGuid().ToString('N')+'.xml')
New-Item -ItemType Directory -Path $blocked | Out-Null
if (Call $p Save ([object[]]@($blocked))) { throw 'Save should report failure' }
if (Test-Path -LiteralPath ($blocked+'.tmp')) { throw 'Temporary save file leaked' }
$form=$iType.GetConstructor($f,$null,[type[]]@([string]),$null).Invoke([object[]]@($blocked.PSObject.BaseObject))
try {
    Call $form ApplySettings ([object[]]@($p)) | Out-Null
    if ([string]::IsNullOrEmpty((Get $form saveWarning).Text)) { throw 'Save failure hidden from user' }
    Remove-Item -LiteralPath $blocked
    Call $form SavePreferences @() | Out-Null
    if (![string]::IsNullOrEmpty((Get $form saveWarning).Text)) { throw 'Warning not cleared after successful retry' }
} finally { $form.Dispose() }
Write-Output 'PASS: save failure visible, no temporary file leaked, warning clears on recovery'
$modes=$mType.GetMethod('All').Invoke($null,@())
$ctor=$uType.GetConstructor([type[]]@($a.GetType('WindowsFish.FishMode'),$mType,[double],$pType))
Put $p Language 'en'
Put $p TextSize 2
foreach ($size in @(@(320,360),@(400,640),@(640,360),@(1040,600))) {
    foreach ($mode in $modes) {
        $localized=Call $mode ForOptions ([object[]]@($p))
        $v=$ctor.Invoke([object[]]@($null,$localized.PSObject.BaseObject,1.0,$p))
        try {
            $v.ClientSize=New-Object Drawing.Size $size[0],$size[1]
            Capture $v ('text-'+$size[0]+'x'+$size[1]+'-'+$mode.Id)
            $layout=Get $v LastLayout
            foreach ($n in @('FirstText','SecondText','Spinner')) {
                $rect=Get $layout $n
                if ($rect.Height -gt 0 -and ($rect.Left -lt 0 -or $rect.Top -lt 0 -or $rect.Right -gt $v.ClientSize.Width+1 -or $rect.Bottom -gt $v.ClientSize.Height+1)) { throw "Clipped text/spinner: $n" }
            }
        } finally { $v.Dispose() }
    }
}
Write-Output 'PASS: large English text and spinner fit narrow/portrait/landscape viewports'
$results=@()
foreach ($eco in @($false,$true)) {
    Put $p EcoMode $eco
    $v=$ctor.Invoke([object[]]@($null,$modes[0],1.0,$p))
    try {
        $v.ClientSize=New-Object Drawing.Size 1040,600
        $v.Show()
        $v.Hide()
        if ((Get $v timer).Enabled) { throw 'Hidden preview still animating' }
        $v.Show()
        if (!(Get $v timer).Enabled) { throw 'Animation not resumed' }
        $report=[FrameProbe]::Measure($v,$FrameSeconds*1000)
        $results += [pscustomobject]@{ Eco=$eco; Metrics=$report }
        Write-Output ($results[-1] | ConvertTo-Json -Compress)
    } finally { $v.Dispose() }
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $out 'frame-report.json') -Encoding UTF8
Write-Output 'PASS: actual Paint intervals measured; hidden animation stops and resumes'
