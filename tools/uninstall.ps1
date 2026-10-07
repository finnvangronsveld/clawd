# Removes Flippy: stops him, deletes his shortcuts, his Settings > Apps entry, his settings and his install folder.
param([string]$Dest = (Join-Path $env:LOCALAPPDATA 'Programs\Flippy'),
      [string]$MenuDir = [Environment]::GetFolderPath('Programs'),
      [string]$StartupDir = [Environment]::GetFolderPath('Startup'),
      [string]$RegName = 'Flippy',
      [string]$DataDir = (Join-Path $env:APPDATA 'Flippy'),
      [switch]$Quiet)   # params are only for testing
Add-Type -AssemblyName System.Windows.Forms
$dest = $Dest

if (-not $Quiet) {
    Get-Process Flippy -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 600
}

foreach ($lnk in (Join-Path $MenuDir 'Flippy.lnk'), (Join-Path $StartupDir 'Flippy.lnk')) {
    if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force }
}
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$RegName"
if (Test-Path $key) { Remove-Item -Path $key -Force }

Set-Location $env:TEMP
if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue }
if ($DataDir -and (Test-Path -LiteralPath $DataDir)) { Remove-Item -LiteralPath $DataDir -Recurse -Force -ErrorAction SilentlyContinue }

if (-not $Quiet) { [System.Windows.Forms.MessageBox]::Show('Flippy has been uninstalled. Bye!', 'Flippy', 'OK', 'Information') | Out-Null }
