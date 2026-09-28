param([switch]$DpiUnaware)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
if (!$DpiUnaware) {
    Add-Type 'using System.Runtime.InteropServices; public class SceneDpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
    [SceneDpi]::SetProcessDPIAware() | Out-Null
}
[Windows.Forms.Application]::EnableVisualStyles()
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root (([string][char]0x6478) + [char]0x9c7c + '.exe')
$a = [Reflection.Assembly]::LoadFrom($exe)
$f = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$s = [Reflection.BindingFlags]'Static,NonPublic,Public'
$pType = $a.GetType('WindowsFish.Preferences')
$iType = $a.GetType('WindowsFish.IntroForm')
$dType = $a.GetType('WindowsFish.SettingsDialog')
$uType = $a.GetType('WindowsFish.UpdateForm')
$mType = $a.GetType('WindowsFish.UpdateScreenMode')
$out = Join-Path $root '.build\test-results'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$path = Join-Path $out ('settings-' + [Guid]::NewGuid().ToString('N') + '.xml')
function Put($obj, $name, $value) { $obj.GetType().GetField($name, $f).SetValue($obj, $value.PSObject.BaseObject) }
function Get($obj, $name) { return $obj.GetType().GetField($name, $f).GetValue($obj) }
function Call($obj, $name, $values) {
    for ($i = 0; $i -lt $values.Length; $i++) {
        if ($null -ne $values[$i]) { $values[$i] = $values[$i].PSObject.BaseObject }
    }
    return $obj.GetType().GetMethod($name, $f).Invoke($obj.PSObject.BaseObject, $values)
}
function Capture($form, $name) {
    [Windows.Forms.Application]::DoEvents()
    $b = New-Object Drawing.Bitmap $form.Width,$form.Height
    try {
        $form.DrawToBitmap($b, (New-Object Drawing.Rectangle 0,0,$form.Width,$form.Height))
        $b.Save((Join-Path $out ($name + '.png')))
    } finally { $b.Dispose() }
}
function CheckLayout($parent) {
    foreach ($c in $parent.Controls) {
        if (!$c.Visible) { continue }
        if ($c.Width -le 0 -or $c.Height -le 0) { throw "Empty control: $($c.Text)" }
        if ($c -is [Windows.Forms.Label] -and $c.AutoSize) {
            if ($c.Bottom -gt $parent.ClientSize.Height -or $c.Right -gt $parent.ClientSize.Width) { throw "Clipped label: $($c.Text)" }
        }
        CheckLayout $c
    }
}
$prefs = [Activator]::CreateInstance($pType, $true)
Put $prefs Scene 'win10'
Put $prefs Palette 'light'
Put $prefs Language 'en'
Put $prefs TextSize 2
Put $prefs DelaySeconds 1
Put $prefs DelayStart $true
Put $prefs EcoMode $true
Call $prefs Save ([object[]]@($path)) | Out-Null
$loaded = $pType.GetMethod('Load', $s).Invoke($null, [object[]]@($path.PSObject.BaseObject))
foreach ($field in @('Scene','Palette','Language','TextSize','DelaySeconds','DelayStart','EcoMode')) {
    if ((Get $prefs $field) -ne (Get $loaded $field)) { throw "Preference roundtrip: $field" }
}
Write-Output 'PASS: new display preferences roundtrip'
$invalid = Join-Path $out 'invalid-settings.xml'
$doc = New-Object Xml.XmlDocument
$doc.LoadXml('<preferences scene="missing" palette="missing" language="missing" textSize="9" delaySeconds="-1" ecoMode="invalid" />')
$doc.Save($invalid)
$defaults = $pType.GetMethod('Load', $s).Invoke($null, [object[]]@($invalid.PSObject.BaseObject))
if ((Get $defaults Scene) -ne 'win11' -or (Get $defaults TextSize) -ne 1 -or (Get $defaults DelaySeconds) -ne 5) { throw 'Invalid preferences not clamped' }
Write-Output 'PASS: invalid preference values fall back to defaults'
$ctor = $dType.GetConstructor($f, $null, [type[]]@($pType), $null)
$dialog = $ctor.Invoke([object[]]@($prefs.PSObject.BaseObject))
try {
    $dialog.Show()
    Capture $dialog ('settings-appearance-' + $DpiUnaware)
    CheckLayout $dialog
    $read = Call $dialog ReadOptions @()
    if ((Get $read Scene) -ne 'win10' -or (Get $read Palette) -ne 'light') { throw 'Dialog did not restore' }
    $tabs = $dialog.Controls[0].Controls | Where-Object { $_ -is [Windows.Forms.TabControl] }
    $tabs.SelectedIndex = 1
    Capture $dialog ('settings-display-' + $DpiUnaware)
    CheckLayout $dialog
    $tabs.SelectedIndex = 2
    Capture $dialog ('settings-about-' + $DpiUnaware)
    $before = [IO.File]::ReadAllText($path)
    Call $dialog ResetDraft @() | Out-Null
    $reset = Call $dialog ReadOptions @()
    if ((Get $reset Scene) -ne 'win11' -or (Get $reset DelaySeconds) -ne 5 -or (Get $reset Palette) -ne 'auto') { throw 'Reset failed' }
    if ([IO.File]::ReadAllText($path) -ne $before) { throw 'Reset wrote settings before Apply' }
    Write-Output 'PASS: dialog values, three tabs, draft reset without writing settings'
} finally { $dialog.Dispose() }
$modes = $mType.GetMethod('All').Invoke($null,@())
$ctor = $uType.GetConstructor([type[]]@($a.GetType('WindowsFish.FishMode'),$mType,[double],$pType))
foreach ($scene in @('win10','win11')) {
    foreach ($palette in @('auto','blue','black','light')) {
        Put $prefs Scene $scene
        Put $prefs Palette $palette
        $mode = Call $modes[0] ForOptions ([object[]]@($prefs))
        $preview = $ctor.Invoke([object[]]@($null,$mode.PSObject.BaseObject,1.0,$prefs.PSObject.BaseObject))
        try {
            $preview.ClientSize = New-Object Drawing.Size 1040,600
            $preview.Show()
            Capture $preview ($scene + '-' + $palette + '-' + $DpiUnaware)
            $expected = if ($palette -eq 'light') { [Drawing.Color]::FromArgb(245,246,248) } elseif ($palette -eq 'blue' -or ($palette -eq 'auto' -and $scene -eq 'win10')) { [Drawing.Color]::FromArgb(0,120,215) } else { [Drawing.Color]::Black }
            if ($preview.BackColor.ToArgb() -ne $expected.ToArgb()) { throw 'Scene palette mismatch' }
            if ((Get $preview timer).Interval -ne 50) { throw 'Energy saving interval mismatch' }
            if ((Get $preview statusFont).SizeInPoints -ne 18) { throw 'Font size mismatch' }
            $first = New-Object Drawing.Bitmap 1040,600
            $second = New-Object Drawing.Bitmap 1040,600
            try {
                $rect = New-Object Drawing.Rectangle 0,0,1040,600
                $preview.DrawToBitmap($first,$rect)
                $wait = [Diagnostics.Stopwatch]::StartNew()
                while ($wait.ElapsedMilliseconds -lt 300) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 5 }
                $preview.DrawToBitmap($second,$rect)
                $region = Get $preview spinnerBounds
                $changed = 0
                for ($x=$region.Left; $x -lt $region.Right; $x+=2) {
                    for ($y=$region.Top; $y -lt $region.Bottom; $y+=2) {
                        if ($first.GetPixel($x,$y).ToArgb() -ne $second.GetPixel($x,$y).ToArgb()) { $changed++ }
                    }
                }
                if ($changed -lt 2) { throw 'Spinner not animating' }
            } finally { $first.Dispose(); $second.Dispose() }
        } finally { $preview.Dispose() }
    }
}
Write-Output 'PASS: both scenes x four palettes, large type, energy saving, moving spinner pixels'
$monitor = $a.GetType('WindowsFish.MonitorSelection').GetMethod('Resolve',$s)
$fallback = $monitor.Invoke($null,[object[]]@([Windows.Forms.Screen]::AllScreens,'missing-device'))
if (!$fallback.Primary) { throw 'Missing monitor fallback failed' }
$form = $iType.GetConstructor($f,$null,[type[]]@([string]),$null).Invoke([object[]]@($path.PSObject.BaseObject))
try {
    $form.Show()
    $explicit = [Windows.Forms.Screen]::AllScreens[-1]
    Put $prefs Monitor $explicit.DeviceName
    Call $form ApplySettings ([object[]]@($prefs)) | Out-Null
    $key = [Windows.Forms.Keys]::Control -bor [Windows.Forms.Keys]::D2
    Call $form HandleShortcut ([object[]]@([Windows.Forms.Keys]$key)) | Out-Null
    if ((Get $form selectedMode).Id -ne 'restart') { throw 'Shortcut failed' }
    (Get $form win10).Checked = $true
    if ((Get (Get $form preferences) Scene) -ne 'win10') { throw 'Homepage scene switch failed' }
    Capture $form ('intro-' + $DpiUnaware)
    (Get $form startButton).PerformClick()
    $wait = [Diagnostics.Stopwatch]::StartNew()
    while ($wait.ElapsedMilliseconds -lt 4000 -and $null -eq (Get $form fishMode)) {
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 10
    }
    $session = Get $form fishMode
    if ($null -eq $session) { throw 'Custom 1-second countdown failed' }
    if ($wait.ElapsedMilliseconds -lt 1000) { throw 'Custom countdown fired early' }
    $windows = Get $session windows
    $status = @($windows | Where-Object { $_.GetType().Name -eq 'UpdateForm' })
    if ($status.Count -ne 1 -or $status[0].Bounds -ne $explicit.Bounds) { throw 'Selected display not used' }
    foreach ($window in $windows) {
        if ($window.GetType().Name -eq 'BlackForm' -and $window.BackColor.ToArgb() -ne [Drawing.Color]::Black.ToArgb()) { throw 'Secondary display not black' }
    }
    Call $session Stop @() | Out-Null
    Write-Output ('PASS: missing monitor fallback, selected screen bounds, shortcuts, scene switching, custom countdown; screens=' + [Windows.Forms.Screen]::AllScreens.Length)
} finally {
    $session = Get $form fishMode
    if ($null -ne $session) { Call $session Stop @() | Out-Null }
    $form.Dispose()
}
$stream = $a.GetManifestResourceStream('Changelog')
if ($null -eq $stream) { throw 'Changelog not embedded' }
$stream.Dispose()
Write-Output ('PASS: embedded changelog; DPI-unaware test=' + $DpiUnaware)
