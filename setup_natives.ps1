$ErrorActionPreference = "Continue"
$outputDir = "src/osu!tag/bin/Debug/net8.0"

# Ensure output dir exists
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    Write-Host "Created output directory: $outputDir"
}

$libs = @(
    @{ Name = "bass"; Targets = @("https://www.un4seen.com/files/bass24.zip"); Zip = "bass24.zip"; Dll = "bass.dll"; IsRoot = $true },
    @{ Name = "bass_fx"; Targets = @("https://www.un4seen.com/files/bass_fx24.zip", "http://jobnik.net/downloads/bass_fx24.zip", "https://jobnik.net/downloads/bass_fx24.zip"); Zip = "bass_fx24.zip"; Dll = "bass_fx.dll"; IsRoot = $false },
    @{ Name = "bassenc"; Targets = @("https://www.un4seen.com/files/bassenc24.zip", "https://www.un4seen.com/files/z/bassenc24.zip"); Zip = "bassenc24.zip"; Dll = "bassenc.dll"; IsRoot = $false },
    @{ Name = "bassenc_mp3"; Targets = @("https://www.un4seen.com/files/bassenc_mp3.zip", "https://www.un4seen.com/files/z/bassenc_mp3.zip"); Zip = "bassenc_mp3.zip"; Dll = "bassenc_mp3.dll"; IsRoot = $false }
)

Write-Host "Downloading BASS libraries to $outputDir..."

foreach ($lib in $libs) {
    if (-not (Test-Path "$outputDir/$($lib.Dll)")) {
        Write-Host "Fetching $($lib.Name)..."
        $zipFile = "$outputDir/$($lib.Zip)"
        
        $downloaded = $false
        foreach ($url in $lib.Targets) {
            try {
                Invoke-WebRequest -Uri $url -OutFile $zipFile -UseBasicParsing -UserAgent "Mozilla/5.0"
                if (Test-Path $zipFile) {
                    $downloaded = $true
                    break
                }
            } catch {
                Write-Host "Failed URL: $url ($($_))"
            }
        }
        
        if (-not $downloaded) {
             Write-Error "Could not download $($lib.Name) from any source."
             continue
        }

        try {
            $tempDir = "$outputDir/temp_$($lib.Name)"
            Expand-Archive -Path $zipFile -DestinationPath $tempDir -Force
            
            # Look for x64 first
            $found = $false
            if (Test-Path "$tempDir/x64/$($lib.Dll)") {
                Copy-Item "$tempDir/x64/$($lib.Dll)" "$outputDir" -Force
                $found = $true
            } elseif (Test-Path "$tempDir/$($lib.Dll)") {
                Copy-Item "$tempDir/$($lib.Dll)" "$outputDir" -Force
                $found = $true
            }
            
            # Special case for bassenc_mp3: it might have lame_enc.dll too
            if ($lib.Name -eq "bassenc_mp3") {
                 if (Test-Path "$tempDir/x64/lame_enc.dll") {
                    Copy-Item "$tempDir/x64/lame_enc.dll" "$outputDir" -Force
                 } elseif (Test-Path "$tempDir/lame_enc.dll") {
                    Copy-Item "$tempDir/lame_enc.dll" "$outputDir" -Force
                 }
            }
            
            if ($found) {
                Write-Host "Installed $($lib.Dll)"
            } else {
                Write-Error "Could not find $($lib.Dll) in zip."
            }
            
            Remove-Item $tempDir -Recurse -Force
            Remove-Item $zipFile -Force
        } catch {
            Write-Error "Failed to extract/install $($lib.Name): $_"
        }
    } else {
        Write-Host "$($lib.Name) already present."
    }
}

Write-Host "Done. Libraries installed."
