param(
    [string]$Version = "0.0.0",
    [string]$PublishPath = "",
    [string]$OutputDir = "build\dist"
)

# If no version provided, try to read AppVersion.Current from source so installer matches the app
if ($Version -eq '0.0.0') {
    try {
        $updateServiceFile = Join-Path -Path (Get-Location) -ChildPath 'Services\UpdateService.cs'
        if (Test-Path $updateServiceFile) {
            $content = Get-Content $updateServiceFile -Raw
            $m = [regex]::Match($content, 'public\s+const\s+string\s+Current\s*=\s*"(?<v>[0-9]+(?:\.[0-9]+)*)"')
            if ($m.Success) {
                $Version = $m.Groups['v'].Value
                Write-Host "Detected app version from source: $Version"
            }
        }
    } catch {
        # ignore - fallback to default
    }
}

Set-StrictMode -Version Latest

if (-not (Test-Path .\OsuTag.csproj)) {
    Write-Error "Run this script from the repository root containing OsuTag.csproj"
    exit 1
}

# Try to locate publish folder if not provided
if ([string]::IsNullOrWhiteSpace($PublishPath)) {
    $default = Join-Path -Path (Join-Path -Path (Get-Location) -ChildPath "bin\Release\net8.0-windows") -ChildPath "win-x64\publish"
    if (Test-Path $default) { $PublishPath = $default }
}

if ([string]::IsNullOrWhiteSpace($PublishPath) -or -not (Test-Path $PublishPath)) {
    Write-Host "Publish output not found. Running dotnet publish..."

    $publishOut = "$PWD\$OutputDir\publish"
    $publishArgs = @('.\OsuTag.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', $publishOut)

    if (![string]::IsNullOrWhiteSpace($Version) -and $Version -ne '0.0.0') {
        # Embed version into built binaries
        $publishArgs += @("-p:Version=$Version", "-p:FileVersion=$Version.0")
    }

    Write-Host "dotnet publish args: $publishArgs"
    $null = dotnet publish @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    try {
        $PublishPath = (Resolve-Path $publishOut -ErrorAction Stop).Path
    } catch {
        Write-Error "Publish output not found at expected location: $publishOut"
        exit 1
    }
}

Write-Host "Using publish path: $PublishPath"

# If a version is requested, ensure the published binaries embed the same version
if (![string]::IsNullOrWhiteSpace($Version) -and $Version -ne '0.0.0') {
    $exePath = Join-Path $PublishPath 'OsuTag.exe'
    $needPublish = $false
    if (Test-Path $exePath) {
        try {
            $pv = (Get-Item $exePath).VersionInfo.ProductVersion
        } catch {
            $pv = ''
        }
        if ($pv -ne $Version) {
            Write-Host "Existing published product version '$pv' differs from requested version '$Version' - republishing with version..."
            $needPublish = $true
        } else {
            Write-Host "Publish already matches requested version: $Version"
        }
    } else {
        Write-Host "Publish binary not found - publishing with version $Version"
        $needPublish = $true
    }

    if ($needPublish) {
        $publishArgs = @('.\OsuTag.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', $PublishPath, "-p:Version=$Version", "-p:FileVersion=$Version.0")
        Write-Host "Republishing with args: $publishArgs"
        dotnet publish @publishArgs
    }
}

# Prepare output
$installerOut = Join-Path $OutputDir "installer"
if (Test-Path $installerOut) { Remove-Item $installerOut -Recurse -Force }
New-Item -Path $installerOut -ItemType Directory | Out-Null

# Create publish ZIP (exclude README/LICENSE/app.ico/installer_wizard.bmp)
if ([string]::IsNullOrWhiteSpace($PublishPath) -or -not (Test-Path $PublishPath)) {
    Write-Error "Publish path '$PublishPath' is invalid. Aborting ZIP creation."
    exit 1
}
$zipName = "OsuTag-$Version.zip"
$zipPath = Join-Path $OutputDir $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$excludes = @('README.md','LICENSE','app.ico','installer_wizard.bmp')
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $PublishPath -Recurse -File | ForEach-Object {
        if ($excludes -contains $_.Name) { return }
        $relative = $_.FullName.Substring($PublishPath.Length).TrimStart('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal)
    }
} finally {
    $zip.Dispose()
}
Write-Host "Created publish zip: $zipPath"

# We intentionally do NOT copy LICENSE, README.md, or app.ico into the publish folder.
# Keep the originals only in the repository root and exclude them from the ZIP and installer payload.
$root = Get-Location
$setupIconPath = $null
$rootIcon = Join-Path $root 'app.ico'
if (Test-Path $rootIcon) {
    $setupIconPath = (Resolve-Path $rootIcon).Path
    Write-Host "Found app icon at repo root: $setupIconPath (will be used for Setup icon but not copied into publish)"
} else {
    Write-Host "No repo app.ico found; installer may not have a custom icon"
} 

# If a logo exists (screenshots/logo.png), convert it to a BMP for use as the wizard image
# Resize to Inno Setup recommended wizard image size (164x314) to ensure it displays correctly
$logoPng = Join-Path $root "screenshots\logo.png"
$resDir = Join-Path $OutputDir 'resources'
if (-not (Test-Path $resDir)) { New-Item -ItemType Directory -Path $resDir | Out-Null }
$wizardBmp = Join-Path $resDir "installer_wizard.bmp"
$wizardWidth = 164
$wizardHeight = 314
if (Test-Path $logoPng) {
    try {
        Add-Type -AssemblyName System.Drawing
        $img = [System.Drawing.Image]::FromFile($logoPng)

        # Create a new bitmap with the recommended size and draw the source image centered (preserving aspect ratio)
        $bmp = $null
        $usedPf = ""
        try {
            $pf = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
            $bmp = New-Object System.Drawing.Bitmap ($wizardWidth, $wizardHeight, ([int]$pf))
            $usedPf = $pf.ToString()
        } catch {
            Write-Warning "Format32bppArgb not supported in this environment; falling back to Format24bppRgb"
            try {
                $pf = [System.Drawing.Imaging.PixelFormat]::Format24bppRgb
                $bmp = New-Object System.Drawing.Bitmap ($wizardWidth, $wizardHeight, ([int]$pf))
                $usedPf = $pf.ToString()
            } catch {
                throw $_
            }
        }
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.Clear([System.Drawing.Color]::White)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

        $scale = [math]::Min($wizardWidth / [double]$img.Width, $wizardHeight / [double]$img.Height)
        $drawW = [int]([math]::Round($img.Width * $scale))
        $drawH = [int]([math]::Round($img.Height * $scale))
        $offsetX = [int](($wizardWidth - $drawW) / 2)
        $offsetY = [int](($wizardHeight - $drawH) / 2)

        $g.DrawImage($img, $offsetX, $offsetY, $drawW, $drawH)
        $g.Dispose()

        $bmp.Save($wizardBmp, [System.Drawing.Imaging.ImageFormat]::Bmp)
        $img.Dispose(); $bmp.Dispose()
        Write-Host "Generated wizard BMP at: $wizardBmp (resized to ${wizardWidth}x${wizardHeight})"
    } catch {
        Write-Warning "Failed to convert logo to BMP for installer wizard: $_"
    }
} else {
    Write-Host "No screenshots/logo.png found, skipping wizard image generation"
} 

# Look for ISCC
$inno = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $inno) {
    $possible = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (Test-Path $possible) { $inno = @{ Path = $possible } }
}

if ($inno) {
    Write-Host "Found Inno Setup compiler at: $($inno.Path)"
    $iss = Join-Path $PSScriptRoot "innosetup\OsuTagInstaller.iss"
    if (-not (Test-Path $iss)) { Write-Error "Inno Setup script not found: $iss"; exit 2 }

    # Ensure publish path is a full resolved path
    try { $PublishPath = (Resolve-Path $PublishPath -ErrorAction Stop).Path } catch { Write-Error "Cannot resolve publish path '$PublishPath'"; exit 1 }

    $args = @("/DMyAppVersion=$Version", "/DSourcePath=`"$PublishPath`"")
    if (Test-Path $wizardBmp) { $args += ("/DWizardBmpPath=`"$wizardBmp`"") }
    if ($setupIconPath) { $args += ("/DSetupIconPath=`"$setupIconPath`"") }
    $args += ("`"$iss`"")
    Write-Host "Running Inno Setup: $($inno.Path) $($args -join ' ')"

    # Capture compiler output for easier debugging
    $outFile = Join-Path $PSScriptRoot 'innosetup-output.log'
    $errFile = Join-Path $PSScriptRoot 'innosetup-error.log'
    if (Test-Path $outFile) { Remove-Item $outFile -Force }
    if (Test-Path $errFile) { Remove-Item $errFile -Force }

    $proc = Start-Process -FilePath $inno.Path -ArgumentList $args -Wait -NoNewWindow -RedirectStandardOutput $outFile -RedirectStandardError $errFile -PassThru
    if ($proc.ExitCode -ne 0) {
        Write-Error "ISCC failed with exit code $($proc.ExitCode). See logs: $outFile, $errFile"
        Write-Host "=== ISCC STDOUT ==="
        Get-Content $outFile -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
        Write-Host "=== ISCC STDERR ==="
        Get-Content $errFile -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
        exit $proc.ExitCode
    }

    # Move built installer(s) to output folder
    $possibleOutputs = @(
        (Join-Path $PSScriptRoot 'innosetup\Output'),
        (Join-Path $PSScriptRoot 'Output')
    )
    $built = $null
    foreach ($dir in $possibleOutputs) {
        if (Test-Path $dir) {
            $candidate = Get-ChildItem -Path $dir -Filter 'OsuTag-Setup-*.exe' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($candidate) { $built = $candidate; break }
        }
    }

    if (-not $built) {
        # Fallback: search recursively under the script root for any matching installer
        $candidate = Get-ChildItem -Path $PSScriptRoot -Filter 'OsuTag-Setup-*.exe' -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($candidate) { $built = $candidate }
    }

    if ($built) {
        Copy-Item -Path $built.FullName -Destination $installerOut -Force
        Write-Host "Installer placed in: $installerOut (found: $($built.FullName))"
    } else {
        Write-Warning "Installer build succeeded but output not found in expected Inno Output folders: $($possibleOutputs -join ', ')"
    }
} else {
    Write-Warning "Inno Setup compiler (ISCC.exe) not found. Publish zip created but installer not built. Install Inno Setup or run on CI to build the .exe." 
}

Write-Host "Done. Artifacts in: $OutputDir"