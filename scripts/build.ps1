$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$build = Join-Path $root '.build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$staged = Join-Path $build 'WindowsFishBuild.exe'
$output = Join-Path $root (([string][char]0x6478) + [char]0x9c7c + '.exe')
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $csc /nologo /target:winexe /optimize+ /platform:x64 /codepage:65001 `
    /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll `
    /reference:"$framework\Microsoft.VisualBasic.dll" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /win32icon:"$root\assets\fish.ico" /win32manifest:"$root\src\app.manifest" `
    /resource:"$root\assets\fish-icon-256.png",FishLogo /resource:"$root\CHANGELOG.md",Changelog `
    /out:$staged $sources
if ($LASTEXITCODE -ne 0) { throw "Compiler failed: $LASTEXITCODE" }
Copy-Item -LiteralPath $staged -Destination $output -Force
Write-Output "Built $output"
Write-Output ([Diagnostics.FileVersionInfo]::GetVersionInfo($output).FileVersion)
