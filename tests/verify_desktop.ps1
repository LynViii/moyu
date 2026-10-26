$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DesktopTestNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr FindWindow(string name, string title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr w, out uint p);
    public static string ForegroundProcess() { uint p; GetWindowThreadProcessId(GetForegroundWindow(),out p); return p == 0 ? "none" : System.Diagnostics.Process.GetProcessById((int)p).ProcessName; }
    public static void TapWindows() { keybd_event(0x5B,0,1,UIntPtr.Zero); System.Threading.Thread.Sleep(80); keybd_event(0x5B,0,3,UIntPtr.Zero); }
    public static void OpenStart() { PostMessage(FindWindow("Shell_TrayWnd",null),0x0112,new IntPtr(0xF130),IntPtr.Zero); }
}
'@
[DesktopTestNative]::SetProcessDPIAware()|Out-Null
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$a=[Reflection.Assembly]::LoadFrom((Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')))
$f=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$s=[Reflection.BindingFlags]'Static,Public,NonPublic'
$out=Join-Path $root '.build\desktop-tests'
New-Item -ItemType Directory -Path $out -Force|Out-Null
function Get($o,$n){return ,$o.GetType().GetField($n,$f).GetValue($o)}
function Prop($o,$n){return $o.GetType().GetProperty($n,$f).GetValue($o,$null)}
function Put($o,$n,$v){$o.GetType().GetField($n,$f).SetValue($o,$v.PSObject.BaseObject)}
function Call($o,$n,$values){
    for($j=0;$j -lt $values.Length;$j++){if($null -ne $values[$j]){$values[$j]=$values[$j].PSObject.BaseObject}}
    return $o.GetType().GetMethod($n,$f).Invoke($o.PSObject.BaseObject,$values)
}
function Static($t,$n,$values){
    for($j=0;$j -lt $values.Length;$j++){if($null -ne $values[$j]){$values[$j]=$values[$j].PSObject.BaseObject}}
    return $t.GetMethod($n,$s).Invoke($null,$values)
}
function Pump($ms){$w=[Diagnostics.Stopwatch]::StartNew();while($w.ElapsedMilliseconds -lt $ms){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 5}}
function Capture($v,$name){$b=New-Object Drawing.Bitmap $v.Width,$v.Height;try{$v.DrawToBitmap($b,(New-Object Drawing.Rectangle 0,0,$v.Width,$v.Height));$b.Save((Join-Path $out ($name+'.png')))}finally{$b.Dispose()}}
$pType=$a.GetType('WindowsFish.Preferences')
$p=[Activator]::CreateInstance($pType,$true)
Put $p DelaySeconds 2
$path=Join-Path $out ('settings-'+[Guid]::NewGuid().ToString('N')+'.xml')
Call $p Save ([object[]]@($path))|Out-Null
$iType=$a.GetType('WindowsFish.IntroForm')
$ctor=$iType.GetConstructor($f,$null,[type[]]@([string],[bool]),$null)
$form=$ctor.Invoke([object[]]@($path.PSObject.BaseObject,$true))
$native=$a.GetType('WindowsFish.NativeDesktop')
try {
    $form.Show();Pump 100
    $form.Close();Pump 100
    if($form.IsDisposed -or $form.Visible -or !(Get $form tray).Visible){throw 'Close-to-tray failed'}
    $form.Reveal();if(!$form.Visible){throw 'Tray restore failed'}
    Call $form HideToTray @()|Out-Null
    $hk=Get $form hotkey
    $selection=1
    while(!(Prop $hk Registered) -and $selection -lt 3){
        Write-Output ('INFO: hotkey option '+$selection+' unavailable; native error='+(Prop $hk ErrorCode))
        $selection++
        Put (Get $form preferences) Hotkey $selection
        Call $form ConfigureHotkey @()|Out-Null
        $hk=Get $form hotkey
    }
    if(!(Prop $hk Registered)){throw ('No free test hotkey; native error='+(Prop $hk ErrorCode))}
    $duplicate=$a.GetType('WindowsFish.GlobalHotkey').GetConstructor($f,$null,[type[]]@([int],[Action]),$null).Invoke([object[]]@($selection.PSObject.BaseObject,[Action]{}))
    try {if(Prop $duplicate Registered){throw 'Hotkey conflict not detected'}}finally{$duplicate.Dispose()}
    [DesktopTestNative]::PostMessage($hk.Handle,0x0312,[IntPtr]0x4D59,[IntPtr]::Zero)|Out-Null
    Pump 100
    $prompt=Get $form launchPrompt
    if($null -eq $prompt -or !$prompt.Visible){throw 'Hotkey did not open countdown prompt'}
    Capture $prompt 'quick-start'
    $prompt.CancelButton.PerformClick();Pump 50
    if($null -ne (Get $form launchTimer) -or $null -ne (Get $form launchPrompt)){throw 'Prompt cancellation failed'}
    Call $form StartLaunch ([object[]]@($true))|Out-Null
    $prompt=Get $form launchPrompt
    $prompt.Close();Pump 50
    if($null -ne (Get $form launchTimer) -or $null -ne (Get $form launchPrompt)){throw 'Prompt close did not cancel countdown'}
    Call $form StartLaunch ([object[]]@($true))|Out-Null
    $prompt=Get $form launchPrompt
    $prompt.AcceptButton.PerformClick();Pump 100
    if($null -eq (Get $form fishMode)){throw 'Start now did not enter fullscreen'}
    Call (Get $form fishMode) Stop @()|Out-Null
    [DesktopTestNative]::PostMessage($hk.Handle,0x0312,[IntPtr]0x4D59,[IntPtr]::Zero)|Out-Null
    Pump 2500
    $session=Get $form fishMode
    if($null -eq $session){throw 'Prompt countdown did not start fullscreen'}
    Call $session Stop @()|Out-Null
    if($form.Visible){throw 'Tray launch should return to tray'}
    Write-Output 'PASS: close/restore tray, registered hotkey/conflict, prompt cancel/start and return-to-tray'
    $form.Reveal()
    Put (Get $form preferences) StartMenuSeconds 1
    Call $form BeginSession @()|Out-Null
    $session=Get $form fishMode
    Call $session ActivatePrimary @()|Out-Null
    Pump 120
    Write-Output ('INFO: foreground before shell check: '+[DesktopTestNative]::ForegroundProcess())
    [DesktopTestNative]::OpenStart()
    $observed=$false;$closed=$false
    $wait=[Diagnostics.Stopwatch]::StartNew()
    while($wait.ElapsedMilliseconds -lt 4500){
        Pump 50
        $menu=Static $native StartMenuForeground @()
        if($menu -ne [IntPtr]::Zero){$observed=$true}
        elseif($observed){$closed=$true;break}
    }
    if(!$observed){Write-Output ('NOT VERIFIED: real-shell Start menu could not stay in foreground; current process='+[DesktopTestNative]::ForegroundProcess())}
    elseif(!$closed -or $null -eq (Get $form fishMode)){throw 'Start menu auto-dismiss failed or exited disguise'}
    else{Write-Output 'PASS: real Windows Start menu auto-dismissed, disguise stayed active'}
    Call $session Stop @()|Out-Null
    $guard=$a.GetType('WindowsFish.StartMenuGuard').GetConstructor($f,$null,[type[]]@([int]),$null).Invoke([object[]]@(1))
    try {
        if((Call $guard ShouldDismiss ([object[]]@([IntPtr]123,0.0))) -or (Call $guard ShouldDismiss ([object[]]@([IntPtr]123,0.9)))){throw 'Start guard dismissed too early'}
        if(!(Call $guard ShouldDismiss ([object[]]@([IntPtr]123,1.0)))){throw 'Start guard missed deadline'}
        if(Call $guard ShouldDismiss ([object[]]@([IntPtr]::Zero,1.1))){throw 'Start guard did not reset after menu closed'}
        if(Call $guard ShouldDismiss ([object[]]@([IntPtr]456,9.0))){throw 'Reopened menu inherited old deadline'}
        if(Static $native DismissStartMenu ([object[]]@([IntPtr]123))){throw 'Guard accepted unrelated foreground window'}
    }finally{$guard.Dispose()}
    Write-Output 'PASS: Start menu deadline/reset and foreign-foreground rejection'
    Call $form ExitApplication @()|Out-Null
    if(!$form.IsDisposed -or $null -ne (Get $form tray) -or $null -ne (Get $form hotkey)){throw 'Explicit exit leaked desktop resources'}
} finally {
    $menu=Static $native StartMenuForeground @()
    if($menu -ne [IntPtr]::Zero){Static $native DismissStartMenu ([object[]]@($menu))|Out-Null}
    $form.Dispose()
}
# Exercise the actual current-user registration, then restore the original value exactly.
$startup=$a.GetType('WindowsFish.StartupRegistration')
$runPath=$startup.GetField('RunPath',$s).GetRawConstantValue()
$valueName=$startup.GetField('ValueName',$s).GetRawConstantValue()
$key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runPath)
$had=$key.GetValueNames() -contains $valueName
$previous=if($had){$key.GetValue($valueName,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)}else{$null}
$kind=if($had){$key.GetValueKind($valueName)}else{[Microsoft.Win32.RegistryValueKind]::String}
try {
    $exe=Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')
    $values=[object[]]@($true,$exe.PSObject.BaseObject,$null)
    if(!$startup.GetMethod('SetEnabled',$s).Invoke($null,$values)){throw $values[2]}
    if($key.GetValue($valueName) -ne ('"'+$exe+'" --silent')){throw 'Startup command not correctly quoted or silent'}
    $values=[object[]]@($false,$exe.PSObject.BaseObject,$null)
    if(!$startup.GetMethod('SetEnabled',$s).Invoke($null,$values) -or $null -ne $key.GetValue($valueName)){throw 'Startup disable failed'}
    Write-Output 'PASS: current-user silent startup enable/disable; original registration restored after test'
} finally {
    if($had){$key.SetValue($valueName,$previous,$kind)}else{$key.DeleteValue($valueName,$false)}
    $key.Dispose()
}
