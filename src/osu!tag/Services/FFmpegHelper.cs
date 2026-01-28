using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Osutag.Services
{
    /// <summary>
    /// Helper service to locate or download FFmpeg/FFplay for audio processing.
    /// </summary>
    public static class FFmpegHelper
    {
        private static string? _cachedFfmpegPath;
        private static string? _cachedFfplayPath;
        private static readonly SemaphoreSlim _downloadLock = new(1, 1);
        private static bool _isDownloading;

        public static bool IsDownloading => _isDownloading;

        public static Task<bool> CheckBinariesExistAsync()
        {
            if (_isDownloading) return Task.FromResult(false);
            
            var localFfmpeg = GetLocalExecutablePath("ffmpeg");
            var localFfplay = GetLocalExecutablePath("ffplay");
            if (File.Exists(localFfmpeg) && File.Exists(localFfplay)) return Task.FromResult(true);

            // Also check PATH as a fallback before saying "not found"
            var path = FindInPath(IsWindows ? "ffmpeg.exe" : "ffmpeg");
            return Task.FromResult(!string.IsNullOrEmpty(path));
        }

        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Gets the path to the FFmpeg executable.
        /// Checks Local -> PATH -> Download.
        /// </summary>
        public static async Task<string> GetFFmpegPathAsync(IProgress<double>? progress = null)
        {
            if (_cachedFfmpegPath != null && File.Exists(_cachedFfmpegPath)) return _cachedFfmpegPath;

            // 1. Check Local (Priority)
            var local = GetLocalExecutablePath("ffmpeg");
            if (File.Exists(local))
            {
                _cachedFfmpegPath = local;
                return local;
            }

            // 2. Check PATH
            var path = FindInPath(IsWindows ? "ffmpeg.exe" : "ffmpeg");
            if (!string.IsNullOrEmpty(path))
            {
                _cachedFfmpegPath = path;
                return path;
            }

            // 3. Download
            await EnsureDownloadedAsync(progress);

            if (File.Exists(local))
            {
                _cachedFfmpegPath = local;
                return local;
            }

            throw new Exception("FFmpeg executable not found even after download.");
        }

        /// <summary>
        /// Gets the path to the FFplay executable.
        /// Checks Local -> PATH -> Download.
        /// </summary>
        public static async Task<string> GetFFplayPathAsync(IProgress<double>? progress = null)
        {
            if (_cachedFfplayPath != null && File.Exists(_cachedFfplayPath)) return _cachedFfplayPath;

            // 1. Check Local (Priority)
            var local = GetLocalExecutablePath("ffplay");
            if (File.Exists(local))
            {
                _cachedFfplayPath = local;
                return local;
            }

            // 2. Check PATH
            var path = FindInPath(IsWindows ? "ffplay.exe" : "ffplay");
            if (!string.IsNullOrEmpty(path))
            {
                _cachedFfplayPath = path;
                return path;
            }

            // 3. Download
            await EnsureDownloadedAsync();

            if (File.Exists(local))
            {
                _cachedFfplayPath = local;
                return local;
            }

            throw new Exception("FFplay executable not found even after download.");
        }

        /// <summary>
        /// Ensures FFmpeg/FFplay binaries are downloaded to the local app data folder.
        /// </summary>
        private static async Task EnsureDownloadedAsync(IProgress<double>? progress = null)
        {
            var localFfmpeg = GetLocalExecutablePath("ffmpeg");
            var localFfplay = GetLocalExecutablePath("ffplay");

            // Fast check before lock
            if (File.Exists(localFfmpeg) && File.Exists(localFfplay)) return;

            _isDownloading = true;
            await _downloadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double check after lock
                if (File.Exists(localFfmpeg) && File.Exists(localFfplay)) return;

                await DownloadFFmpegAsync(progress).ConfigureAwait(false);
            }
            finally
            {
                _downloadLock.Release();
                _isDownloading = false;
            }
        }

        private static string GetLocalExecutablePath(string name)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var ffmpegDir = Path.Combine(appData, "osu!tag", "ffmpeg");
            
            var exeName = IsWindows ? $"{name}.exe" : name;
            return Path.Combine(ffmpegDir, exeName);
        }

        /// <summary>
        /// Downloads and extracts FFmpeg binaries (including ffplay).
        /// </summary>
        public static async Task DownloadFFmpegAsync(IProgress<double>? progress = null)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var ffmpegDir = Path.Combine(appData, "osu!tag", "ffmpeg");
            Directory.CreateDirectory(ffmpegDir);

            string downloadUrl;
            string archiveName = "ffmpeg.zip";

            if (IsWindows)
            {
                // Windows build (includes ffplay)
                downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Mac build
                downloadUrl = "https://evermeet.cx/ffmpeg/getrelease/zip";
            }
            else
            {
                throw new PlatformNotSupportedException("Linux automatic download not supported. Please install ffmpeg package.");
            }

            var archivePath = Path.Combine(ffmpegDir, archiveName);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "osu-tag-app/1.0"); // GitHub requires User-Agent
            httpClient.Timeout = TimeSpan.FromMinutes(10); 

            try
            {
                Debug.WriteLine($"Downloading FFmpeg from {downloadUrl}...");
                using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                
                await using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var totalReadBytes = 0L;
                var readBytes = 0;

                while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, readBytes).ConfigureAwait(false);
                    totalReadBytes += readBytes;

                    if (totalBytes.HasValue)
                    {
                        // Download progress: 0.0 - 0.9 (Leave 10% for extraction)
                        progress?.Report((double)totalReadBytes / totalBytes.Value * 0.9);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to download: {ex.Message}");
                throw new Exception($"Failed to download FFmpeg: {ex.Message}");
            }

            try
            {
                progress?.Report(0.92); // Starting extraction
                
                // Unique extract folder to avoid collision
                var extractDir = Path.Combine(ffmpegDir, "extract_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(extractDir);

                try
                {
                    ZipFile.ExtractToDirectory(archivePath, extractDir);
                    progress?.Report(0.98); // Extraction nearly done

                    // Search recursively for binaries
                    var ffmpegExeName = IsWindows ? "ffmpeg.exe" : "ffmpeg";
                    var ffplayExeName = IsWindows ? "ffplay.exe" : "ffplay";

                    var foundFfmpeg = FindFile(extractDir, ffmpegExeName);
                    var foundFfplay = FindFile(extractDir, ffplayExeName);

                    if (!string.IsNullOrEmpty(foundFfmpeg))
                        File.Copy(foundFfmpeg, GetLocalExecutablePath("ffmpeg"), true);

                    if (!string.IsNullOrEmpty(foundFfplay))
                        File.Copy(foundFfplay, GetLocalExecutablePath("ffplay"), true);

                    // Unix permissions
                    if (!IsWindows)
                    {
                        if (File.Exists(GetLocalExecutablePath("ffmpeg")))
                            Process.Start("chmod", $"+x \"{GetLocalExecutablePath("ffmpeg")}\"")?.WaitForExit();
                        if (File.Exists(GetLocalExecutablePath("ffplay")))
                            Process.Start("chmod", $"+x \"{GetLocalExecutablePath("ffplay")}\"")?.WaitForExit();
                    }
                }
                finally
                {
                    // Cleanup extract folder
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                }

                // Cleanup archive
                if (File.Exists(archivePath)) File.Delete(archivePath);
                
                progress?.Report(1.0); // Done
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to extract: {ex.Message}");
                throw new Exception($"Failed to extract FFmpeg: {ex.Message}");
            }
        }

        private static string? FindInPath(string executable)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;

            var paths = pathEnv.Split(Path.PathSeparator);
            var extensions = IsWindows ? new[] { "", ".exe", ".cmd", ".bat" } : new[] { "" };

            foreach (var path in paths)
            {
                foreach (var ext in extensions)
                {
                    try
                    {
                        var fullPath = Path.Combine(path.Trim(), executable + ext);
                        if (File.Exists(fullPath)) return fullPath;
                    }
                    catch { } // Ignore invalid paths in PATH
                }
            }
            return null;
        }

        private static string? FindFile(string directory, string fileName)
        {
            try
            {
                foreach (var file in Directory.GetFiles(directory, fileName, SearchOption.AllDirectories))
                    return file;
            }
            catch { }
            return null;
        }
    }
}
