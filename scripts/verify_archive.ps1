param([string]$ArchivePath=$PSScriptRoot)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ArchivePath).Path.TrimEnd('\')
$seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$count=0
foreach($line in Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt') -Encoding UTF8){
    $parts=$line -split '  ',2
    if($parts.Length -ne 2 -or $parts[0] -notmatch '^[0-9a-f]{64}$'){throw 'Invalid checksum line'}
    $path=[IO.Path]::GetFullPath((Join-Path $root $parts[1]))
    if(!$path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or !$seen.Add($path)){throw 'Unsafe or duplicate archive path'}
    if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw ('Missing archive file: '+$parts[1])}
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $parts[0]){throw ('Checksum mismatch: '+$parts[1])}
    $count++
}
if($count -lt 7){throw 'Archive checksum list is incomplete'}
$entries=@(Get-ChildItem -LiteralPath $root -Recurse -Force)
foreach($entry in $entries){
    if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Archive contains an unexpected reparse point'}
    if(!$entry.PSIsContainer -and $entry.FullName -ne (Join-Path $root 'SHA256SUMS.txt') -and !$seen.Contains($entry.FullName)){
        throw ('Unexpected archive file: '+$entry.FullName)
    }
}
Write-Output ('PASS: '+$count+' archive files match SHA-256 checksums')
