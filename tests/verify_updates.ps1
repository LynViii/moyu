param([switch]$Live,[switch]$LiveOnly)
if($LiveOnly){$Live=$true}
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Runtime.Serialization
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')
if(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($exe)) -ErrorAction SilentlyContinue){throw 'Close the running application before updater testing.'}
$a=[Reflection.Assembly]::LoadFrom($exe)
$f=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$s=[Reflection.BindingFlags]'Static,Public,NonPublic'
$service=$a.GetType('WindowsFish.UpdateService')
$planType=$a.GetType('WindowsFish.UpdatePlan')
$out=Join-Path $root '.build\update-tests'
New-Item -ItemType Directory -Path $out -Force|Out-Null
function Put($o,$n,$v){$o.GetType().GetField($n,$f).SetValue($o,$v.PSObject.BaseObject)}
function Prop($o,$n){return $o.GetType().GetProperty($n,$f).GetValue($o,$null)}
function Static($t,$n,$values){
    for($j=0;$j -lt $values.Length;$j++){if($null -ne $values[$j]){$values[$j]=$values[$j].PSObject.BaseObject}}
    return $t.GetMethod($n,$s).Invoke($null,$values)
}
function Call($o,$n,$values){return $o.GetType().GetMethod($n,$f).Invoke($o.PSObject.BaseObject,$values)}
function MustReject($script,$label){$rejected=$false;try{& $script}catch{$rejected=$true};if(!$rejected){throw ('Accepted invalid input: '+$label)}}
function Hash($path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
$preferences=Join-Path $env:LOCALAPPDATA 'Moyu\settings.xml'
$preferencesBefore=if(Test-Path -LiteralPath $preferences){Hash $preferences}else{''}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$good=Join-Path $out 'good.exe';$bad=Join-Path $out 'bad.exe'
& $csc /nologo /target:winexe /platform:x64 /codepage:65001 /out:$good (Join-Path $PSScriptRoot 'UpdateFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Fixture build failed'}
& $csc /nologo /target:winexe /platform:x64 /codepage:65001 /define:FAIL_UPDATE /out:$bad (Join-Path $PSScriptRoot 'UpdateFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Failure fixture build failed'}
$goodHash=Hash $good
$v=Static $service ParseVersion ([object[]]@('v0.5.1'))
Static $service VerifyCandidate ([object[]]@($good,$goodHash,$v))|Out-Null
MustReject {Static $service VerifyCandidate ([object[]]@($good,('0'*64),$v))} 'hash mismatch'
MustReject {Static $service VerifyCandidate ([object[]]@($bad,(Hash $bad),$v))} 'wrong version'
MustReject {Static $service ReadChecksum ([object[]]@('invalid  moyu.exe','moyu.exe'))} 'bad checksum'
MustReject {Static $service ReadChecksum ([object[]]@(($goodHash+'  moyu.exe'+[Environment]::NewLine+$goodHash+'  moyu.exe'),'moyu.exe'))} 'duplicate checksum'
foreach($url in @('http://github.com/LynViii/moyu/releases/download/v1/moyu.exe','https://evil.example/moyu.exe','https://github.com/other/repo/releases/download/v1/moyu.exe')){
    MustReject {Static $service ValidateAssetUrl ([object[]]@($url))} 'foreign source'
}
$metadata=@{tag_name='v0.5.1';body='fixture';draft=$false;prerelease=$false;assets=@(
    @{name='moyu.exe';browser_download_url='https://github.com/LynViii/moyu/releases/download/v0.5.1/moyu.exe';size=(Get-Item $good).Length;digest=('sha256:'+$goodHash)},
    @{name='SHA256SUMS.txt';browser_download_url='https://github.com/LynViii/moyu/releases/download/v0.5.1/SHA256SUMS.txt';size=80})}
$release=Static $service ParseRelease ([object[]]@(($metadata|ConvertTo-Json -Depth 4 -Compress)))
if((Prop $release Tag) -ne 'v0.5.1'){throw 'Release JSON not parsed'}
$metadata.prerelease=$true
MustReject {Static $service ParseRelease ([object[]]@(($metadata|ConvertTo-Json -Depth 4 -Compress)))} 'prerelease'
Write-Output 'PASS: release parser, source restrictions, stable version, hashes, PE identity and version'

function Stop-TestApp($target){
    $p=Start-Process -FilePath $target -ArgumentList '--exit' -PassThru -WindowStyle Hidden
    try{if(!$p.WaitForExit(10000)){throw 'Test app exit handoff failed'}}finally{$p.Dispose()}
    foreach($running in Get-Process -ErrorAction SilentlyContinue){
        try{$matches=$running.Path -eq $target}catch{$matches=$false}
        if($matches){try{if(!$running.WaitForExit(10000)){throw 'Test app did not finish closing'}}finally{$running.Dispose()}}
    }
}
if(!$LiveOnly){
foreach($case in @('success','rollback','cancel','tampered','locked')){
    $folder=Join-Path $out ([Guid]::NewGuid().ToString('N'))
    $targetFolder=Join-Path $out ('target-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $folder,$targetFolder|Out-Null
    $target=Join-Path $targetFolder (([string][char]0x6478)+[char]0x9c7c+' test.exe')
    Copy-Item -LiteralPath $exe -Destination $target
    $oldHash=Hash $target
    $source=if($case -eq 'rollback'){$bad}else{$good}
    Copy-Item -LiteralPath $source -Destination (Join-Path $folder 'moyu.exe')
    Copy-Item -LiteralPath $exe -Destination (Join-Path $folder 'Moyu.UpdateHelper.exe')
    $parent=$null;$helper=$null;$lock=$null
    try{
        $parent=Start-Process -FilePath $target -ArgumentList '--silent' -PassThru -WindowStyle Hidden
        if(!$parent.WaitForInputIdle(10000)){throw 'Parent app did not initialize'}
        Start-Sleep -Milliseconds 250
        $parent.Refresh();if($parent.HasExited){throw 'Parent exited too early'}
        $plan=[Activator]::CreateInstance($planType,$true)
        Put $plan DirectoryPath $folder;Put $plan Target $target;Put $plan OldHash $oldHash
        Put $plan NewHash $(if($case -eq 'tampered'){'0'*64}else{Hash $source})
        Put $plan Version $(if($case -eq 'rollback'){'0.5.2'}else{'0.5.1'})
        Put $plan Token ([Guid]::NewGuid().ToString('N'));Put $plan ProcessId $parent.Id
        Put $plan ProcessTicks $parent.StartTime.ToUniversalTime().Ticks;Put $plan Silent $true
        Call $plan Save @()|Out-Null
        $token=$planType.GetField('Token',$f).GetValue($plan)
        $args='--apply-update "'+(Prop $plan PlanPath)+'" '+$token
        $helper=Start-Process -FilePath (Prop $plan Helper) -ArgumentList $args -PassThru -WindowStyle Hidden
        $wait=[Diagnostics.Stopwatch]::StartNew()
        while(!(Test-Path -LiteralPath (Prop $plan Prepared)) -and !(Test-Path -LiteralPath (Prop $plan Result)) -and $wait.ElapsedMilliseconds -lt 10000){Start-Sleep -Milliseconds 50}
        if($case -ne 'tampered'){
            if(!(Test-Path -LiteralPath (Prop $plan Prepared))){throw ('Helper not prepared: '+$case)}
            if((Hash $target) -ne $oldHash){throw 'Target changed before parent exit'}
            if($case -eq 'locked'){$lock=[IO.File]::Open($target,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)}
            if($case -eq 'cancel'){
                Static ($a.GetType('WindowsFish.UpdateInstaller')) Cancel ([object[]]@($plan))|Out-Null
            }else{Stop-TestApp $target;if(!$parent.WaitForExit(10000)){throw 'Parent still running'}}
        }
        if(!$helper.WaitForExit(20000)){throw ('Helper timeout: '+$case)}
        [xml]$result=Get-Content -LiteralPath (Prop $plan Result) -Raw -Encoding UTF8
        if($case -eq 'success'){
            if($result.result.status -ne 'success' -or (Hash $target) -ne $goodHash -or (Get-Content (Join-Path $targetFolder 'fixture-started.txt') -Raw) -ne 'silent'){throw 'Successful update not installed/restarted'}
        }elseif($case -eq 'rollback'){
            if($result.result.status -ne 'rolledBack' -or (Hash $target) -ne $oldHash){throw ('Rollback failed: '+$result.result.message)}
            Start-Sleep -Milliseconds 500
            Stop-TestApp $target
        }elseif($case -eq 'locked'){
            if($result.result.status -ne 'error' -or (Hash $target) -ne $oldHash){throw 'Locked target changed unexpectedly'}
            $lock.Dispose();$lock=$null
            Start-Sleep -Milliseconds 500
            Stop-TestApp $target
        }else{
            if($result.result.status -ne 'error' -or (Hash $target) -ne $oldHash -or $parent.HasExited){throw ('Rejected update altered parent: '+$case)}
            Stop-TestApp $target
        }
        $preferencesAfter=if(Test-Path -LiteralPath $preferences){Hash $preferences}else{''}
        if($preferencesAfter -ne $preferencesBefore){throw 'Update helper changed user preferences'}
        Write-Output ('PASS: real updater helper '+$case+', expected EXE and preferences preserved')
    }finally{
        if($null -ne $lock){$lock.Dispose()}
        foreach($p in @($helper,$parent)){
            if($null -ne $p){try{if(!$p.WaitForExit(2000)){try{$p.Kill()}catch{$p.Refresh();if(!$p.HasExited){throw}};$p.WaitForExit(5000)|Out-Null}}finally{$p.Dispose()}}
        }
        foreach($p in Get-Process -ErrorAction SilentlyContinue){try{if($p.Path -eq $target){$p.Kill();$p.WaitForExit()}}catch{}}
    }
}
}
if($Live){
    $release=(Static $service CheckAsync ([object[]]@([Threading.CancellationToken]::None))).GetAwaiter().GetResult()
    Write-Output ('PASS: live GitHub release check '+(Prop $release Tag))
    $download=(Static $service DownloadAsync ([object[]]@($release,$null,[Threading.CancellationToken]::None))).GetAwaiter().GetResult()
    try{Write-Output ('PASS: live release download and verification '+(Hash $download))}finally{Remove-Item -LiteralPath $download;Remove-Item -LiteralPath (Split-Path $download -Parent)}
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [Windows.Forms.Application]::EnableVisualStyles()
    $introType=$a.GetType('WindowsFish.IntroForm')
    $form=$introType.GetConstructor($f,$null,[type[]]@([string]),$null).Invoke([object[]]@((Join-Path $out 'ui-settings.xml').PSObject.BaseObject))
    try{
        $form.Show()
        Call $form OpenUpdateDialog @()|Out-Null
        $dialog=$introType.GetField('updateWindow',$f).GetValue($form)
        if($form.Enabled -or $null -eq $dialog){throw 'Update window did not guard homepage'}
        [Windows.Forms.Application]::DoEvents()
        $button=$dialog.GetType().GetField('primary',$f).GetValue($dialog)
        $wait=[Diagnostics.Stopwatch]::StartNew()
        while(!$button.Enabled -and $wait.ElapsedMilliseconds -lt 25000){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 15}
        $status=$dialog.GetType().GetField('status',$f).GetValue($dialog)
        if(!$button.Enabled -or $status.Text -notmatch '^\u5f53\u524d\u5df2\u662f\u6700\u65b0\u7248\u672c\u3002$'){throw ('Update UI check failed: '+$status.Text)}
        foreach($size in @(@(560,420),@(420,340))){
            $dialog.ClientSize=New-Object Drawing.Size $size[0],$size[1]
            [Windows.Forms.Application]::DoEvents()
            if($button.Bottom -gt $button.Parent.ClientSize.Height -or $button.Width -lt 80){throw 'Update button clipped'}
            $bitmap=New-Object Drawing.Bitmap $dialog.Width,$dialog.Height
            try{$dialog.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle 0,0,$dialog.Width,$dialog.Height));$bitmap.Save((Join-Path $out ('update-ui-'+$size[0]+'.png')))}finally{$bitmap.Dispose()}
        }
        $dialog.Close()
        if(!$form.Enabled){throw 'Homepage remained disabled after closing updater'}
        Write-Output 'PASS: live update window status, narrow layout and homepage guard recovery'
    }finally{$form.Dispose()}
}
