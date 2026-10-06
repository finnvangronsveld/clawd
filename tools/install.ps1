# Installs Clawd for the current user (no admin needed) and starts him.
#   - copies him to %LOCALAPPDATA%\Programs\Clawd
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

    # stop any Clawd that's already running (he'll be restarted from the new place)
    if (-not $Quiet) { Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
        Where-Object { $_.CommandLine -match 'clawd\.ps1' -and $_.ProcessId -ne $PID } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue } }
    Start-Sleep -Milliseconds 500

    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item (Join-Path $src 'clawd.ps1') $dest -Force
    Copy-Item (Join-Path $src 'clawd.ico') $dest -Force
    Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $dest -Force
    Get-ChildItem $dest | Unblock-File          # files from a downloaded zip are marked as "from the internet"

    $ps1 = Join-Path $dest 'clawd.ps1'
    $ico = Join-Path $dest 'clawd.ico'
    $vbs = Join-Path $dest 'launch.vbs'
    $cmd = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -STA -File ""' + $ps1 + '""'
    'CreateObject("WScript.Shell").Run "' + $cmd + '", 0, False' | Set-Content -Path $vbs -Encoding ASCII

    $sh = New-Object -ComObject WScript.Shell
    foreach ($lnkPath in (Join-Path $MenuDir 'Clawd.lnk'),
                         (Join-Path $StartupDir 'Clawd.lnk')) {
        $l = $sh.CreateShortcut($lnkPath)
        $l.TargetPath = Join-Path $env:WINDIR 'System32\wscript.exe'
        $l.Arguments = '"' + $vbs + '"'
        $l.WorkingDirectory = $dest
        $l.IconLocation = "$ico,0"
        $l.Description = 'Clawd, the little orange desktop pet'
        $l.Save()
    }

    # Settings > Apps entry
    $key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$RegName"
    New-Item -Path $key -Force | Out-Null
    $un = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $dest 'uninstall.ps1') + '"'
    $props = @{
        DisplayName = 'Clawd'; DisplayIcon = $ico; Publisher = 'finnvangronsveld'; DisplayVersion = '1.0'
        InstallLocation = $dest; UninstallString = $un; QuietUninstallString = $un
        URLInfoAbout = 'https://github.com/finnvangronsveld/clawd'
    }
    foreach ($p in $props.Keys) { New-ItemProperty -Path $key -Name $p -Value $props[$p] -PropertyType String -Force | Out-Null }
    New-ItemProperty -Path $key -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $key -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

    if ($Quiet) { return }
    Start-Process wscript.exe -ArgumentList ('"' + $vbs + '"')

    [System.Windows.Forms.MessageBox]::Show(
        "Clawd is installed! Look at the bottom of your screen.`n`n" +
        "- Right-click him for his menu`n- He starts with Windows (you can turn that off in his menu)`n" +
        "- Find him in the Start menu as 'Clawd' if you ever send him away`n`n" +
        "Uninstall any time from Settings > Apps.",
        'Clawd', 'OK', 'Information') | Out-Null
} catch {
    [System.Windows.Forms.MessageBox]::Show("Clawd couldn't be installed:`n`n$_", 'Clawd', 'OK', 'Error') | Out-Null
    exit 1
}
