$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root (([string][char]0x6478) + [char]0x9c7c + '.exe')
if (!(Test-Path -LiteralPath $exe)) { throw 'Build the executable first.' }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
$release = Join-Path $root "docs\releases\$version.md"
if (!(Test-Path -LiteralPath $release)) { throw "Release notes missing for $version" }
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$zip = Join-Path $dist "moyu-$version-win-x64.zip"
$downloadExe = Join-Path $dist 'moyu.exe'
Copy-Item -LiteralPath $exe -Destination $downloadExe -Force
Compress-Archive -LiteralPath @($exe, $release) -DestinationPath $zip -Force
$hashes = foreach ($file in @($downloadExe, $zip)) {
    $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($file)
}
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'), $hashes, (New-Object Text.UTF8Encoding $false))
Write-Output $zip
Write-Output $hashes
