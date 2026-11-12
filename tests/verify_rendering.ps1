$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'RenderingProbe.cs') -ReferencedAssemblies System.Windows.Forms,System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$a=[Reflection.Assembly]::LoadFrom((Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')))
$f=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$pType=$a.GetType('WindowsFish.Preferences')
$type=$a.GetType('WindowsFish.UpdateForm')
$mode=$a.GetType('WindowsFish.UpdateScreenMode').GetMethod('All').Invoke($null,@())[0]
$ctor=$type.GetConstructor([type[]]@($a.GetType('WindowsFish.FishMode'),$mode.GetType(),[double],$pType))
function Get($o,$n){return $o.GetType().GetField($n,$f).GetValue($o)}
function NewView($scene,$palette){
    $p=[Activator]::CreateInstance($pType,$true)
    $pType.GetField('Scene',$f).SetValue($p,$scene)
    $pType.GetField('Palette',$f).SetValue($p,$palette)
    return $ctor.Invoke([object[]]@($null,$mode,1.0,$p))
}
foreach($scene in @('win11','win10')){
    foreach($palette in @('black','light')){
        $view=NewView $scene $palette
        $first=New-Object Drawing.Bitmap 720,480
        $partial=New-Object Drawing.Bitmap 720,480
        $expected=New-Object Drawing.Bitmap 720,480
        try{
            $view.ClientSize=New-Object Drawing.Size 720,480
            $clock=Get $view previewClock;$clock.Reset()
            [RenderingProbe]::Paint($view,$first,$view.ClientRectangle)
            $g=[Drawing.Graphics]::FromImage($partial)
            try{$g.DrawImageUnscaled($first,0,0)}finally{$g.Dispose()}
            $dirty=Get $view spinnerBounds
            $clock.Start();Start-Sleep -Milliseconds 150;$clock.Stop()
            [RenderingProbe]::Paint($view,$partial,$dirty)
            if([RenderingProbe]::Differences($first,$partial,$dirty,$false) -lt 8){throw 'Partial spinner is blank or static'}
            if([RenderingProbe]::Differences($first,$partial,$dirty,$true) -ne 0){throw 'Partial repaint touched static text/background'}
            [RenderingProbe]::Paint($view,$expected,$view.ClientRectangle)
            if([RenderingProbe]::Differences($partial,$expected,$view.ClientRectangle,$false) -ne 0){throw ('Partial/full rendering differs: '+$scene+' '+$palette)}
            $view.ClientSize=New-Object Drawing.Size 640,360
            if($null -ne (Get $view spinnerBuffer)){throw 'Resize retained old graphics buffer'}
        }finally{$view.Dispose();$first.Dispose();$partial.Dispose();$expected.Dispose()}
    }
}
Write-Output 'PASS: Win10/Win11 dark/light dirty frames match complete frames; static pixels untouched; resize releases buffer'
$handle=[Diagnostics.Process]::GetCurrentProcess().Handle
$before=[RenderingProbe]::GetGuiResources($handle,0)
for($i=0;$i -lt 40;$i++){
    $view=NewView 'win11' 'black'
    $bitmap=New-Object Drawing.Bitmap 720,480
    try{
        $view.ClientSize=$bitmap.Size
        [RenderingProbe]::Paint($view,$bitmap,$view.ClientRectangle)
        [RenderingProbe]::Paint($view,$bitmap,(Get $view spinnerBounds))
    }finally{$view.Dispose();$bitmap.Dispose()}
}
[GC]::Collect();[GC]::WaitForPendingFinalizers()
$after=[RenderingProbe]::GetGuiResources($handle,0)
if($after -gt $before+4){throw ('GDI resources grew across 40 disposal cycles: '+$before+' -> '+$after)}
Write-Output ('PASS: 40 create/render/dispose cycles; GDI handles '+$before+' -> '+$after)
