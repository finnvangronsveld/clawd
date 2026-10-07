# Installs Clawd for the current user (no admin needed) and starts him.
#   - copies Clawd.exe to %LOCALAPPDATA%\Programs\Clawd
#   - Start menu entry + "start with Windows" + an entry in Settings > Apps for uninstalling
param([string]$Dest = (Join-Path $env:LOCALAPPDATA 'Programs\Clawd'),
      [string]$MenuDir = [Environment]::GetFolderPath('Programs'),
      [string]$StartupDir = [Environment]::GetFolderPath('Startup'),
      [string]$RegName = 'Clawd',
      [switch]$Quiet)   # params are only for testing; double-clicking uses the defaults
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

try {
    $src  = Split-Path $PSScriptRoot -Parent
    $dest = $Dest

    # stop any Clawd that's already running (v2 exe, or the old v1 script)
    if (-not $Quiet) {
        Get-Process Clawd -ErrorAction SilentlyContinue | Stop-Process -Force
        Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
            Where-Object { $_.CommandLine -match 'clawd\.ps1' -and $_.ProcessId -ne $PID } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Milliseconds 600
    }

    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item (Join-Path $src 'Clawd.exe') $dest -Force
    Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $dest -Force
    # clean up a v1 install in the same place
    foreach ($old in 'clawd.ps1', 'launch.vbs', 'clawd.ico') { $p = Join-Path $dest $old; if (Test-Path $p) { Remove-Item -LiteralPath $p -Force } }
    Get-ChildItem $dest | Unblock-File          # files from a downloaded zip are marked as "from the internet"

    $exe = Join-Path $dest 'Clawd.exe'
    $sh = New-Object -ComObject WScript.Shell
    foreach ($lnkPath in (Join-Path $MenuDir 'Clawd.lnk'), (Join-Path $StartupDir 'Clawd.lnk')) {
        $l = $sh.CreateShortcut($lnkPath)
        $l.TargetPath = $exe
        $l.WorkingDirectory = $dest
        $l.IconLocation = "$exe,0"
        $l.Description = 'Clawd, the little orange desktop buddy'
        $l.Save()
    }

    # Settings > Apps entry
    $key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$RegName"
    New-Item -Path $key -Force | Out-Null
    $un = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $dest 'uninstall.ps1') + '"'
    $props = @{
        DisplayName = 'Clawd'; DisplayIcon = "$exe,0"; Publisher = 'finnvangronsveld'; DisplayVersion = '2.0'
        InstallLocation = $dest; UninstallString = $un; QuietUninstallString = $un
        URLInfoAbout = 'https://github.com/finnvangronsveld/clawd'
    }
    foreach ($p in $props.Keys) { New-ItemProperty -Path $key -Name $p -Value $props[$p] -PropertyType String -Force | Out-Null }
    New-ItemProperty -Path $key -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $key -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

    if ($Quiet) { return }
    Start-Process $exe

    [System.Windows.Forms.MessageBox]::Show(
        "Clawd is installed! Look at the bottom of your screen.`n`n" +
        "- Right-click him for his menu (and Settings)`n- Drag and throw him, feed him snacks, drop files on him`n" +
        "- He starts with Windows (turn that off in Settings)`n- Find him in the Start menu as 'Clawd' if you ever send him away`n`n" +
        "Uninstall any time from Settings > Apps.",
        'Clawd', 'OK', 'Information') | Out-Null
} catch {
    [System.Windows.Forms.MessageBox]::Show("Clawd couldn't be installed:`n`n$_", 'Clawd', 'OK', 'Error') | Out-Null
    exit 1
}
