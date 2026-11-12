param([string]$Executable,[string]$Label='current')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$Executable=Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if($Label -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Use a simple ASCII measurement label.'}
$out=Join-Path $root ('.build\performance\'+$Label)
New-Item -ItemType Directory -Path $out -Force|Out-Null
$probe=Join-Path $out 'ResourceProbe.exe'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:$probe (Join-Path $PSScriptRoot 'ResourceProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Resource probe compilation failed'}
$reports=@()
foreach($case in @(@('win11','normal'),@('win11','eco'),@('win10','normal'),@('win11','hidden'))){
    $result=Join-Path $out ($case[0]+'-'+$case[1]+'.json')
    $arguments='"'+$Executable+'" "'+$result+'" '+$case[0]+' '+$case[1]
    $p=Start-Process -FilePath $probe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    try{
        if(!$p.WaitForExit(20000)){ $p.Kill(); $p.WaitForExit(); throw 'Resource probe timeout' }
        if($p.ExitCode -ne 0){throw ('Probe failed: '+(Get-Content -LiteralPath ($result+'.error.txt') -Raw))}
    }finally{$p.Dispose()}
    $report=Get-Content -LiteralPath $result -Raw -Encoding UTF8|ConvertFrom-Json
    if($case[1] -eq 'hidden' -and $report.Paints -gt 1){throw 'Hidden animation still drawing'}
    if($case[1] -ne 'hidden' -and $report.PaintFps -lt 8){throw 'Animation did not render enough frames'}
    $reports+=$report
    Write-Output ($report|ConvertTo-Json -Compress)
}
$reports|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $out 'summary.json') -Encoding UTF8
