param(
    [string]$InstallerPath = "build\dist\installer\OsuTag-Setup-1.2.3.exe",
    [string]$InstallDir = "C:\Program Files\osu!tag",
    [int]$WaitMs = 3000
)

if (-not (Test-Path $InstallerPath)) { Write-Error "Installer not found: $InstallerPath"; exit 2 }

# 1) Install silently
Start-Process -FilePath $InstallerPath -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/LOG="C:\temp\osutag_install.log"' -Wait
if (!(Test-Path (Join-Path $InstallDir 'OsuTag.exe'))) { Write-Error 'Install failed: exe not found'; exit 2 }

# 2) Launch app and wait a bit, then close
$proc = Start-Process -FilePath (Join-Path $InstallDir 'OsuTag.exe') -PassThru
Start-Sleep -Milliseconds $WaitMs
try { $proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(2000) } catch { $proc.Kill() }

# 3) Basic smoke check (file timestamps, license exists)
if (!(Test-Path (Join-Path $InstallDir 'LICENSE'))) { Write-Error 'LICENSE missing' ; exit 3 }

# 4) Uninstall silently
$uninst = Get-ChildItem $InstallDir -Filter 'unins*.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $uninst) { Write-Error 'Uninstaller not found' ; exit 4 }
Start-Process -FilePath $uninst.FullName -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait

# 5) Verify uninstall cleaned up
if (Test-Path $InstallDir) { Write-Error 'Uninstall left files behind' ; exit 5 }

Write-Host 'Installer smoke test succeeded'; exit 0