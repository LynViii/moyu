$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ExitTestNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr w,uint m,IntPtr p,IntPtr l);
}
'@
[ExitTestNative]::SetProcessDPIAware()|Out-Null
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$a=[Reflection.Assembly]::LoadFrom((Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')))
$f=[Reflection.BindingFlags]'Instance,Public,NonPublic';$s=[Reflection.BindingFlags]'Static,Public,NonPublic'
$binding=$a.GetType('WindowsFish.ExitBinding');$matcherType=$a.GetType('WindowsFish.ExitMatcher')
function Get($o,$n){return ,$o.GetType().GetField($n,$f).GetValue($o)}
function Put($o,$n,$v){$o.GetType().GetField($n,$f).SetValue($o,$v.PSObject.BaseObject)}
function Call($o,$n,$values){for($j=0;$j -lt $values.Length;$j++){if($null -ne $values[$j]){$values[$j]=$values[$j].PSObject.BaseObject}};return $o.GetType().GetMethod($n,$f).Invoke($o.PSObject.BaseObject,$values)}
function Matcher($pattern){return $matcherType.GetConstructor($f,$null,[type[]]@([string]),$null).Invoke([object[]]@($pattern.PSObject.BaseObject))}
function Down($m,$key,$time){return Call $m KeyDown ([object[]]@([Windows.Forms.Keys]$key,[double]$time))}
function Up($m,$key){Call $m KeyUp ([object[]]@([Windows.Forms.Keys]$key))|Out-Null}
function Pump($ms){$w=[Diagnostics.Stopwatch]::StartNew();while($w.ElapsedMilliseconds -lt $ms){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 3}}
function Tap($window,$key){$handle=$window.Handle;[ExitTestNative]::PostMessage($handle,0x100,[IntPtr]$key,[IntPtr]1)|Out-Null;Pump 8;[ExitTestNative]::PostMessage($handle,0x101,[IntPtr]$key,[IntPtr]1)|Out-Null;Pump 8}
$m=Matcher '27;27'
if((Down $m 27 0) -or (Down $m 27 100)){throw 'Held key matched a two-press binding'}
Up $m 27
if(!(Down $m 27 200)){throw 'Double Esc did not match'}
$m=Matcher '38;38;40'
$matched=$false;$t=0
foreach($key in @(38,38,38,40)){$matched=Down $m $key $t;Up $m $key;$t+=100}
if(!$matched){throw 'Overlapping prefix not handled'}
$m=Matcher '27;27'
Down $m 27 0|Out-Null;Up $m 27
if(Down $m 27 2000){throw 'Sequence did not time out'}
Up $m 27
if(!(Down $m 27 2100)){throw 'Sequence could not restart'}
$m=Matcher '393297'
if(!(Down $m 393297 0)){throw 'Modifier chord failed'}
foreach($pattern in @('38','262259','262153','0','-1','27;')){
    $values=[object[]]@($pattern.PSObject.BaseObject,$null)
    if($binding.GetMethod('TryParse',$s).Invoke($null,$values)){throw ('Invalid binding accepted: '+$pattern)}
}
Write-Output 'PASS: exit chords, sequence overlap/timeout, auto-repeat rejection and reserved-key validation'
$out=Join-Path $root '.build\exit-tests';New-Item -ItemType Directory -Path $out -Force|Out-Null
$pType=$a.GetType('WindowsFish.Preferences');$iType=$a.GetType('WindowsFish.IntroForm')
$p=[Activator]::CreateInstance($pType,$true);Put $p ExitPattern '38;40;37;39';Put $p DismissStartMenu $false
$path=Join-Path $out ('settings-'+[Guid]::NewGuid().ToString('N')+'.xml')
Call $p Save ([object[]]@($path))|Out-Null
$form=$iType.GetConstructor($f,$null,[type[]]@([string]),$null).Invoke([object[]]@($path.PSObject.BaseObject))
try{
    $form.Show();Call $form BeginSession @()|Out-Null;Pump 80
    $session=Get $form fishMode;$windows=Get $session windows
    $window=@($windows|Where-Object{$_.GetType().Name -eq 'UpdateForm'})[0]
    Tap $window 27
    if($null -eq (Get $form fishMode)){throw 'Esc exited a custom-bound session'}
    foreach($key in @(38,40,37,39)){Tap $window $key}
    if($null -ne (Get $form fishMode) -or !$form.Visible){throw 'Arrow-key exit did not return to homepage'}
    Write-Output 'PASS: actual fullscreen message filter ignores Esc and accepts configured arrow sequence'
}finally{$form.Dispose()}
$dType=$a.GetType('WindowsFish.SettingsDialog')
$d=$dType.GetConstructor($f,$null,[type[]]@($pType),$null).Invoke([object[]]@($p))
try{
    $d.Show();Pump 30
    $tabs=$d.Controls[0].Controls|Where-Object{$_ -is [Windows.Forms.TabControl]};$tabs.SelectedIndex=2
    (Get $d exitChoice).SelectedIndex=4
    (Get $d recordExit).PerformClick();Pump 20
    $recorder=Get $d exitRecorder
    foreach($key in @(38,38,40,40)){Tap $recorder $key}
    (Get $d recordExit).PerformClick()
    $recorded=Call $d ReadOptions @()
    if((Get $recorded ExitPattern) -ne '38;38;40;40'){throw 'Custom recorder did not capture arrows'}
    $bitmap=New-Object Drawing.Bitmap $d.Width,$d.Height
    try{$d.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle 0,0,$d.Width,$d.Height));$bitmap.Save((Join-Path $out 'exit-settings.png'))}finally{$bitmap.Dispose()}
    $d.AcceptButton.PerformClick()
    if($d.DialogResult -ne [Windows.Forms.DialogResult]::OK){throw 'Valid custom binding not applied'}
    Write-Output 'PASS: real recorder captures key presses and settings apply'
}finally{$d.Dispose()}
$promptType=$a.GetType('WindowsFish.LaunchPrompt')
$longBinding=(@('458875')*12)-join ';'
$prompt=$promptType.GetConstructor($f,$null,[type[]]@([string],[string],[int],[string]),$null).Invoke([object[]]@('win11','Restarting',3,$longBinding))
try{
    $prompt.Show();Pump 40
    $rootPanel=$prompt.Controls[0]
    $hint=$rootPanel.GetControlFromPosition(0,2)
    $counter=$rootPanel.GetControlFromPosition(0,1)
    $buttons=$rootPanel.GetControlFromPosition(0,3)
    if($counter.Height -lt 45 -or $hint.Bottom -gt $buttons.Top -or $buttons.Bottom -gt $rootPanel.ClientSize.Height - $rootPanel.Padding.Bottom){throw 'Long exit sequence clipped countdown or buttons'}
    $bitmap=New-Object Drawing.Bitmap $prompt.Width,$prompt.Height
    try{$prompt.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle 0,0,$prompt.Width,$prompt.Height));$bitmap.Save((Join-Path $out 'long-exit-prompt.png'))}finally{$bitmap.Dispose()}
    Write-Output 'PASS: maximum-length exit sequence fits countdown prompt'
}finally{$prompt.Dispose()}
