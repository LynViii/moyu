$ErrorActionPreference = "Stop"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source = Join-Path $PSScriptRoot "WindowsFish.cs"
$icon = Join-Path $PSScriptRoot "fish.ico"
$buildOutput = Join-Path $PSScriptRoot "WindowsFishBuild.exe"
$appName = [string]([char]0x6478) + [string]([char]0x9c7c) + ".exe"
$output = Join-Path $PSScriptRoot $appName

& $csc /nologo /target:winexe /optimize+ /platform:x64 /codepage:65001 `
  /reference:System.dll `
  /reference:System.Xml.Linq.dll `
  /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Microsoft.VisualBasic.dll" `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /win32icon:$icon `
  /win32manifest:"$PSScriptRoot\app.manifest" `
  /resource:"$PSScriptRoot\fish-icon-256.png",FishLogo `
  /out:$buildOutput `
  $source (Join-Path $PSScriptRoot "VersionInfo.cs")

if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

Copy-Item -LiteralPath $buildOutput -Destination $output -Force
Remove-Item -LiteralPath $buildOutput -Force

Write-Host "Built $output"
