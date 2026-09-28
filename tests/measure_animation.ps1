param([string]$Executable, [int]$Seconds = 30)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type 'using System.Runtime.InteropServices; public class MeasureDpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
[MeasureDpi]::SetProcessDPIAware() | Out-Null
[Windows.Forms.Application]::EnableVisualStyles()
Add-Type -Path (Join-Path $PSScriptRoot 'FrameProbe.cs') -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Core
if (!$Executable) { $Executable = Join-Path (Split-Path $PSScriptRoot -Parent) (([string][char]0x6478) + [char]0x9c7c + '.exe') }
$a=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Executable))
$mode=$a.GetType('WindowsFish.UpdateScreenMode').GetMethod('All').Invoke($null,@())[0]
$v=[Activator]::CreateInstance($a.GetType('WindowsFish.UpdateForm'),[object[]]@($null,$mode))
try {
    $v.ClientSize=New-Object Drawing.Size 1040,600
    $result=[FrameProbe]::Measure($v,$Seconds*1000)
    [pscustomobject]@{ Version=[Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).ProductVersion; Metrics=$result } | ConvertTo-Json -Depth 4
} finally { $v.Dispose() }
