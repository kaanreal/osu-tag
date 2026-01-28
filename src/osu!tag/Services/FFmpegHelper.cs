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

        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Gets the path to the FFmpeg executable.
        /// Checks Local -> PATH -> Download.
        /// </summary>
        public static async Task<string> GetFFmpegPathAsync()
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
            await EnsureDownloadedAsync();

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
        public static async Task<string> GetFFplayPathAsync()
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
        private static async Task EnsureDownloadedAsync()
        {
            var localFfmpeg = GetLocalExecutablePath("ffmpeg");
            var localFfplay = GetLocalExecutablePath("ffplay");

            // Fast check before lock
            if (File.Exists(localFfmpeg) && File.Exists(localFfplay)) return;

            await _downloadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double check after lock
                if (File.Exists(localFfmpeg) && File.Exists(localFfplay)) return;

                await DownloadFFmpegAsync().ConfigureAwait(false);
            }
            finally
            {
                _downloadLock.Release();
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
        private static async Task DownloadFFmpegAsync()
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

                await using var fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to download: {ex.Message}");
                throw new Exception($"Failed to download FFmpeg: {ex.Message}");
            }

            try
            {
                // Unique extract folder to avoid collision
                var extractDir = Path.Combine(ffmpegDir, "extract_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(extractDir);

                try
                {
                    ZipFile.ExtractToDirectory(archivePath, extractDir);

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
