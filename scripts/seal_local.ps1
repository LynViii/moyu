param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $root -Parent
$destination=[IO.Path]::GetFullPath($Destination)
if(!$destination.StartsWith($workspace+'\',[StringComparison]::OrdinalIgnoreCase) -or $destination.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){
    throw 'Archive must be inside the workspace and outside the source checkout.'
}
if(Test-Path -LiteralPath $destination){throw 'Archive destination already exists; never overwrite a sealed archive.'}
$dirty=git -C $root status --porcelain
if($LASTEXITCODE -ne 0 -or $dirty){throw 'Commit all source/documentation changes before sealing.'}
$exe=Join-Path $root (([string][char]0x6478)+[char]0x9c7c+'.exe')
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
$commit=git -C $root rev-parse HEAD
$tagCommit=git -C $root rev-parse ('v'+$version+'^{commit}')
if($LASTEXITCODE -ne 0 -or $commit -ne $tagCommit){throw 'Release tag does not match HEAD.'}
New-Item -ItemType Directory -Path $destination|Out-Null
$incomplete=Join-Path $destination 'INCOMPLETE.txt'
[IO.File]::WriteAllText($incomplete,'Archive verification has not completed.')
foreach($name in @('release','source','history','evidence')){New-Item -ItemType Directory -Path (Join-Path $destination $name)|Out-Null}
foreach($name in @('moyu.exe',('moyu-'+$version+'-win-x64.zip'),'SHA256SUMS.txt')){
    Copy-Item -LiteralPath (Join-Path $root ('dist\'+$name)) -Destination (Join-Path $destination ('release\'+$name))
}
Copy-Item -LiteralPath (Join-Path $root 'docs\HANDOFF.md') -Destination (Join-Path $destination 'START_HERE.md')
Copy-Item -LiteralPath (Join-Path $root ('docs\VALIDATION_'+$version+'.md')) -Destination (Join-Path $destination 'VALIDATION.md')
Copy-Item -LiteralPath (Join-Path $root ('docs\PERFORMANCE_'+$version+'.md')) -Destination (Join-Path $destination 'PERFORMANCE.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify_archive.ps1') -Destination (Join-Path $destination 'verify_archive.ps1')
$source=Join-Path $destination ('source\moyu-'+$version+'-source.zip')
git -C $root archive --format=zip --output=$source HEAD
if($LASTEXITCODE -ne 0){throw 'Source snapshot failed'}
$bundle=Join-Path $destination 'history\moyu-all.bundle'
git -C $root bundle create $bundle --all
if($LASTEXITCODE -ne 0){throw 'Git bundle creation failed'}
git -C $root bundle verify $bundle
if($LASTEXITCODE -ne 0){throw 'Git bundle verification failed'}
foreach($label in @('before','before_repeat','final')){
    $report=Join-Path $root ('.build\performance\'+$label+'\summary.json')
    Copy-Item -LiteralPath $report -Destination (Join-Path $destination ('evidence\resources-'+$label+'.json'))
}
foreach($name in @('frame-report.json','full-regression-0.5.1.log')){
    $path=Join-Path $root ('.build\acceptance\'+$name)
    if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $destination ('evidence\'+$name))}
}
Copy-Item -LiteralPath (Join-Path $root '.build\performance\idle.json') -Destination (Join-Path $destination 'evidence\idle.json')
Copy-Item -LiteralPath (Join-Path $root '.build\performance\baseline\moyu-0.5.0.exe') -Destination (Join-Path $destination 'history\moyu-0.5.0-baseline.exe')
$legacy=Join-Path $workspace 'work'
if(Test-Path -LiteralPath $legacy){
    $legacyFiles=@(Get-ChildItem -LiteralPath $legacy -File|ForEach-Object{$_.FullName})
    if($legacyFiles.Count){Compress-Archive -LiteralPath $legacyFiles -DestinationPath (Join-Path $destination 'history\legacy-work.zip')}
}
$restore=Join-Path $root ('.build\archive-restore-'+[Guid]::NewGuid().ToString('N'))
git clone --quiet $bundle $restore
if($LASTEXITCODE -ne 0){throw 'Bundle restore clone failed'}
if((git -C $restore rev-parse HEAD) -ne $commit){throw 'Restored commit differs'}
git -C $restore remote set-url origin https://github.com/LynViii/moyu.git
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $restore 'scripts\build.ps1')
if($LASTEXITCODE -ne 0){throw 'Restored source build failed'}
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $restore 'tests\verify_executable.ps1')
if($LASTEXITCODE -ne 0){throw 'Restored executable smoke test failed'}
$utf8=New-Object Text.UTF8Encoding $false
$restoreResult=[ordered]@{Commit=$commit;Version=$version;BundleVerified=$true;CloneVerified=$true;BuildVerified=$true;ExecutableSmokeTest=$true;ByteReproducibleBuildClaimed=$false}
[IO.File]::WriteAllText((Join-Path $destination 'evidence\restore-check.json'),($restoreResult|ConvertTo-Json),$utf8)
Remove-Item -LiteralPath $incomplete
[IO.File]::WriteAllText((Join-Path $destination 'SEALED.txt'),('Moyu '+$version+"`r`nCommit "+$commit+"`r`nLocal frozen snapshot. Original checkout preserved. GitHub remains writable."),$utf8)
$files=@(Get-ChildItem -LiteralPath $destination -Recurse -File|Sort-Object FullName|ForEach-Object{
    [ordered]@{Path=$_.FullName.Substring($destination.Length+1).Replace('\','/');Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$manifest=[ordered]@{Format=1;Version=$version;Commit=$commit;Tag=('v'+$version);Repository='https://github.com/LynViii/moyu';CreatedUtc=[DateTime]::UtcNow.ToString('o');Files=$files}
[IO.File]::WriteAllText((Join-Path $destination 'manifest.json'),($manifest|ConvertTo-Json -Depth 5),$utf8)
$checksums=@(Get-ChildItem -LiteralPath $destination -Recurse -File|Sort-Object FullName|ForEach-Object{
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_.FullName.Substring($destination.Length+1).Replace('\','/')
})
[IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'),$checksums,$utf8)
& (Join-Path $destination 'verify_archive.ps1') -ArchivePath $destination
Write-Output ('SEALED: '+$destination)
