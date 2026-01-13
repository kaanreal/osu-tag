# osu!tag

<p align="center">
  <img src="src/OsuTag/Assets/app.ico" alt="osu!tag Logo" width="128">
</p>

<p align="center">
  <strong>Convert your osu! beatmaps into properly tagged MP3 files</strong>
</p>
<p align="center">
  <strong>Cross-Platform: Windows • macOS • Linux</strong>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#installation">Installation</a> •
  <a href="#usage">Usage</a> •
  <a href="#license">License</a>
</p>

---

## Features

* 🎵 **Batch Convert** – Convert multiple beatmaps to tagged MP3s at once
* 🏷️ **Auto-Tagging** – Automatically applies ID3 tags (Artist, Title, Album Art)
* 🥞 **Beautiful UI** – Glassmorphism design with immersive full-window welcome screen
* 🖱️ **Context Actions** – Right-click cards to View Online, Open Folder, or Export Backgrounds
* 🖼️ **Cover Art** – Extracts and embeds beatmap backgrounds as album covers
* 🔍 **Smart Scan** – Only scans new beatmap folders on subsequent launches
* 🎧 **Audio Preview** – Hover over maps to preview the audio
* 💾 **Remember Path** – Automatically loads your Songs folder on startup
* 📊 **Anonymous Telemetry** – Optional usage statistics to help improve osu!tag
* 📈 **Companella! Integration** – Automatically detects [Companella!](https://github.com/Leinadix/companella) for play count stats
* 🔄 **Update Checker** – Checks for new releases on startup

## Installation

### Requirements

* **Windows**: Windows 10/11
* **macOS**: macOS 10.15+ (Catalina or newer)
* **Linux**: Distributions supporting GLibc (Ubuntu, Fedora, etc.)
* **.NET 8.0 Runtime** (Optional if using self-contained installer)

### Download

1. Go to the [Releases](../../releases) page
2. Download the version for your OS:
   - **Windows**: `osu-tag-win-installer.exe` or `.zip`
   - **macOS**: `osu-tag-mac.zip`
   - **Linux**: `osu-tag-linux.zip`
3. Run the application!

## Usage

1. **Select Songs Folder** – Use the beautiful welcome screen to select your osu! Songs folder.
2. **Scan & Select** – The app intelligently scans your maps. Click to select cards.
3. **Context Menu** – Right-click any card to view details on osu.ppy.sh or open its folder.
4. **Convert** – Click "Start Version" to generate tagged MP3s.

## Tech Stack

* **Framework**: Avalonia UI (.NET 8.0) - Cross Platform!
* **Image Processing**: SixLabors.ImageSharp
* **Audio Tagging**: TagLibSharp
* **Audio Playback**: LibVLCSharp / NSSound

## License

MIT License – See [LICENSE](LICENSE) for details.

---

<p align="center">
  Made with ❤️ for the osu! community
</p>
