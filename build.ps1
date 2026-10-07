# Builds Flippy.exe with the C# compiler that ships with Windows (.NET Framework 4.x) - nothing to install.
#   powershell -ExecutionPolicy Bypass -File build.ps1
#   -Out bin\Dev.exe   builds somewhere else (so a running copy doesn't lock the output)
param([string]$Out = 'Flippy.exe')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$fw = Split-Path $csc
$out = Join-Path $PSScriptRoot $Out
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$src = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$args = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', "/out:$out", '/win32icon:flippy.ico',
          '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Management.dll',
          "/r:$fw\System.Runtime.WindowsRuntime.dll") + $src
& $csc @args
if ($LASTEXITCODE -ne 0) { throw "build failed" }
"built $out ($([int]((Get-Item $out).Length / 1KB)) KB)"
