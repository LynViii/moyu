$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')
if(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($exe)) -ErrorAction SilentlyContinue){throw 'Close the running app before idle measurement.'}
$p=Start-Process -FilePath $exe -ArgumentList '--silent' -PassThru -WindowStyle Hidden
try{
    if(!$p.WaitForInputIdle(10000)){throw 'Silent startup failed'}
    Start-Sleep -Milliseconds 1000
    $p.Refresh();if($p.HasExited -or $p.MainWindowHandle -ne 0){throw 'App is not silently resident'}
    $cpu=$p.TotalProcessorTime;$clock=[Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds 10
    $elapsed=$clock.Elapsed.TotalSeconds
    $cpuMs=($p.TotalProcessorTime-$cpu).TotalMilliseconds
    $p.Refresh()
    $report=[ordered]@{Version=$p.MainModule.FileVersionInfo.ProductVersion;Seconds=$elapsed;CpuMs=$cpuMs;CpuOneCorePercent=($cpuMs/$elapsed/10);PrivateMiB=($p.PrivateMemorySize64/1MB);WorkingMiB=($p.WorkingSet64/1MB);Handles=$p.HandleCount;VisibleMainWindow=($p.MainWindowHandle -ne 0)}
    $out=Join-Path $root '.build\performance\idle.json'
    $report|ConvertTo-Json|Set-Content -LiteralPath $out -Encoding UTF8
    Write-Output ($report|ConvertTo-Json -Compress)
}finally{
    if(!$p.HasExited){$exit=Start-Process -FilePath $exe -ArgumentList '--exit' -PassThru -WindowStyle Hidden;try{$exit.WaitForExit(10000)|Out-Null;$p.WaitForExit(10000)|Out-Null}finally{$exit.Dispose()}}
    $p.Dispose()
}
