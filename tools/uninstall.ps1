# Removes Clawd: stops him, deletes his shortcuts, his Settings > Apps entry, his settings and his install folder.
param([string]$Dest = (Join-Path $env:LOCALAPPDATA 'Programs\Clawd'),
      [string]$MenuDir = [Environment]::GetFolderPath('Programs'),
      [string]$StartupDir = [Environment]::GetFolderPath('Startup'),
      [string]$RegName = 'Clawd',
      [string]$DataDir = (Join-Path $env:APPDATA 'Clawd'),
      [switch]$Quiet)   # params are only for testing
Add-Type -AssemblyName System.Windows.Forms
$dest = $Dest

if (-not $Quiet) {
    Get-Process Clawd -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
        Where-Object { $_.CommandLine -match 'clawd\.ps1' } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 600
}

foreach ($lnk in (Join-Path $MenuDir 'Clawd.lnk'), (Join-Path $StartupDir 'Clawd.lnk')) {
    if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force }
}
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$RegName"
if (Test-Path $key) { Remove-Item -Path $key -Force }

Set-Location $env:TEMP
if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue }
if ($DataDir -and (Test-Path -LiteralPath $DataDir)) { Remove-Item -LiteralPath $DataDir -Recurse -Force -ErrorAction SilentlyContinue }

if (-not $Quiet) { [System.Windows.Forms.MessageBox]::Show('Clawd has been uninstalled. Bye!', 'Clawd', 'OK', 'Information') | Out-Null }
