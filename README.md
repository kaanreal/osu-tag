# osu!tag

<p align="center">
  <img src="src/OsuTag/Assets/logo.png" alt="osu!tag Logo" width="128">
</p>

<p align="center">
  <strong>Convert osu! beatmaps into properly tagged MP3 files</strong><br>
  Windows • macOS • Linux
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#installation">Installation</a> •
  <a href="#usage">Usage</a> •
  <a href="#screenshots">Screenshots</a> •
  <a href="#license">License</a>
</p>

---

## Features

- **Batch Export** - Process and tag multiple beatmaps simultaneously.
- **Metadata Tagging** - Automatically applies ID3 tags including Artist, Title, and Album info.
- **Cover Art Extraction** - Extracts beatmap backgrounds and embeds them as album art.
- **Modern Interface** - Clean glassmorphism design built with Avalonia.
- **Context Actions** - Right-click support to view maps online, open local folders, or export backgrounds.
- **Incremental Scanning** - Only scans for new beatmap folders on subsequent launches.
- **Audio Previews** - Preview map audio by hovering over cards.
- **Path Persistence** - Automatically remembers and loads your Songs folder.
- **Companella! Integration** - Detects ([Companella!](https://github.com/Leinadix/companella)) for play count statistics.
- **Update Checker** - Notifies you of new releases on startup.
- **Anonymous Telemetry** - Optional usage statistics to assist with development.

## Screenshots

### Main Interface

![Main Interface](screenshots/main.png)

### Map Selection

![Map Selection](screenshots/selection.png)

### Settings

![Settings](screenshots/settings.png)

## Installation

### Requirements

- **Windows**: Windows 10/11
- **macOS**: macOS 10.15 or newer
- **Linux**: Distributions supporting GLibc (Ubuntu, Fedora, etc.)
- **.NET 8.0 Runtime** (Optional if using the self-contained installer)

### Download

1. Go to the Releases page.
2. Download the version for your OS:
    - Windows: osu-tag-win-installer.exe or .zip
    - macOS: osu-tag-mac.zip (Needs ([Sentinel](https://github.com/alienator88/Sentinel)))
    - Linux: osu-tag-linux.zip
3. Launch the application.

## Usage

1. **Select Songs Folder** - Point the application to your osu! Songs directory.
2. **Scan and Select** - The app scans your maps; click cards to select them for export.
3. **Context Menu** - Right-click any card to view details on the osu! website or open the file location.
4. **Convert** - Click "Start" to generate the tagged MP3 files.

## Tech Stack

- **Framework**: Avalonia UI (.NET 8.0)
- **Image Processing**: SixLabors.ImageSharp
- **Audio Tagging**: TagLibSharp
- **Audio Playback**: LibVLCSharp / NSSound

## License

MIT License – See LICENSE for details.

---

<p align="center">
  Built for the osu! community
</p>
