# Installs Flippy for the current user (no admin needed) and starts him.
#   - copies Flippy.exe to %LOCALAPPDATA%\Programs\Flippy
#   - Start menu entry + "start with Windows" + an entry in Settings > Apps for uninstalling
#   - cleans up an install from before the rename (old folder, shortcuts and Settings > Apps entry)
param([string]$Dest = (Join-Path $env:LOCALAPPDATA 'Programs\Flippy'),
      [string]$MenuDir = [Environment]::GetFolderPath('Programs'),
      [string]$StartupDir = [Environment]::GetFolderPath('Startup'),
      [string]$RegName = 'Flippy',
      [string]$OldDest = (Join-Path $env:LOCALAPPDATA 'Programs\Clawd'),     # migration: the app's old name
      [string]$OldRegName = 'Clawd',
      [switch]$Quiet)   # params are only for testing; double-clicking uses the defaults
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$oldName = 'Clawd'     # migration only: the app's name before the rename

try {
    $src  = Split-Path $PSScriptRoot -Parent
    $dest = $Dest

    # stop any running copy (this one, or the old one under its old name)
    if (-not $Quiet) {
        Get-Process Flippy, $oldName -ErrorAction SilentlyContinue | Stop-Process -Force
        Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
            Where-Object { $_.CommandLine -match ($oldName + '\.ps1') -and $_.ProcessId -ne $PID } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Milliseconds 600
    }

    # remove the old install (pre-rename): folder, shortcuts, Settings > Apps entry. Settings/needs are migrated by the app.
    if ($OldDest -and (Test-Path -LiteralPath $OldDest)) { Remove-Item -LiteralPath $OldDest -Recurse -Force -ErrorAction SilentlyContinue }
    foreach ($lnk in (Join-Path $MenuDir "$oldName.lnk"), (Join-Path $StartupDir "$oldName.lnk")) { if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force } }
    $oldKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$OldRegName"
    if ($OldRegName -and (Test-Path $oldKey)) { Remove-Item -Path $oldKey -Force }

    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item (Join-Path $src 'Flippy.exe') $dest -Force
    if (Test-Path (Join-Path $src 'flippy.ico')) { Copy-Item (Join-Path $src 'flippy.ico') $dest -Force }
    Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $dest -Force
    Get-ChildItem $dest | Unblock-File          # files from a downloaded zip are marked as "from the internet"

    $exe = Join-Path $dest 'Flippy.exe'
    $sh = New-Object -ComObject WScript.Shell
    foreach ($lnkPath in (Join-Path $MenuDir 'Flippy.lnk'), (Join-Path $StartupDir 'Flippy.lnk')) {
        $l = $sh.CreateShortcut($lnkPath)
        $l.TargetPath = $exe
        $l.WorkingDirectory = $dest
        $l.IconLocation = "$exe,0"
        $l.Description = 'Flippy, your little desktop buddy'
        $l.Save()
    }

    # Settings > Apps entry
    $key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$RegName"
    New-Item -Path $key -Force | Out-Null
    $un = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $dest 'uninstall.ps1') + '"'
    $props = @{
        DisplayName = 'Flippy'; DisplayIcon = "$exe,0"; Publisher = 'finnvangronsveld'; DisplayVersion = '2.4'
        InstallLocation = $dest; UninstallString = $un; QuietUninstallString = $un
        URLInfoAbout = 'https://github.com/finnvangronsveld/flippy'
    }
    foreach ($p in $props.Keys) { New-ItemProperty -Path $key -Name $p -Value $props[$p] -PropertyType String -Force | Out-Null }
    New-ItemProperty -Path $key -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $key -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

    if ($Quiet) { return }
    Start-Process $exe

    [System.Windows.Forms.MessageBox]::Show(
        "Flippy is installed! Look at the bottom of your screen.`n`n" +
        "- Right-click him for his menu (change pet, settings...)`n- Drag and throw him, feed him snacks, drop files on him`n" +
        "- He starts with Windows (turn that off in Settings)`n- Find him in the Start menu as 'Flippy' if you ever send him away`n`n" +
        "Uninstall any time from Settings > Apps.",
        'Flippy', 'OK', 'Information') | Out-Null
} catch {
    [System.Windows.Forms.MessageBox]::Show("Flippy couldn't be installed:`n`n$_", 'Flippy', 'OK', 'Error') | Out-Null
    exit 1
}
