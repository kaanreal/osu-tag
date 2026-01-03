using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OsuTag.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string ReleaseName { get; set; } = "";
        public DateTime PublishedAt { get; set; }
        public bool IsNewer { get; set; }
        public long FileSize { get; set; }
        public string FileName { get; set; } = "";
    }

    public class UpdateService
    {
        // GitHub repository info
        private const string GITHUB_OWNER = "kaanreal";
        private const string GITHUB_REPO = "osu-tag";
        private const string GITHUB_API_URL = "https://api.github.com/repos/{0}/{1}/releases/latest";
        
        private static readonly HttpClient _httpClient = new HttpClient();
        
        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OsuTag-UpdateChecker");
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// Check for updates from GitHub releases
        /// </summary>
        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                var url = string.Format(GITHUB_API_URL, GITHUB_OWNER, GITHUB_REPO);
                var response = await _httpClient.GetStringAsync(url);
                
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                var tagName = root.GetProperty("tag_name").GetString() ?? "";
                var releaseName = root.GetProperty("name").GetString() ?? "";
                var body = root.GetProperty("body").GetString() ?? "";
                var publishedAt = root.GetProperty("published_at").GetDateTime();
                var htmlUrl = root.GetProperty("html_url").GetString() ?? "";
                
                string downloadUrl = htmlUrl;
                string fileName = "";
                long fileSize = 0;
                
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var assetName = asset.GetProperty("name").GetString() ?? "";
                        if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? htmlUrl;
                            fileName = assetName;
                            fileSize = asset.GetProperty("size").GetInt64();
                            break;
                        }
                        else if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(fileName))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? htmlUrl;
                            fileName = assetName;
                            fileSize = asset.GetProperty("size").GetInt64();
                        }
                    }
                }
                
                var latestVersion = NormalizeVersion(tagName);
                var currentVersion = NormalizeVersion(AppVersion.Current);
                
                return new UpdateInfo
                {
                    Version = tagName,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = body,
                    ReleaseName = releaseName,
                    PublishedAt = publishedAt,
                    IsNewer = CompareVersions(latestVersion, currentVersion) > 0,
                    FileSize = fileSize,
                    FileName = fileName
                };
            }
            catch (Exception)
            {
                // Log error or handle appropriately in release
                return null;
            }
        }
        
        /// <summary>
        /// Download update with progress reporting
        /// </summary>
        public static async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<(long downloaded, long total)>? progress = null)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "OsuTagUpdate");
                Directory.CreateDirectory(tempDir);
                
                var fileName = !string.IsNullOrEmpty(updateInfo.FileName) 
                    ? updateInfo.FileName 
                    : $"OsuTag_{updateInfo.Version}.exe";
                var downloadPath = Path.Combine(tempDir, fileName);
                
                foreach (var file in Directory.GetFiles(tempDir))
                {
                    try { File.Delete(file); } catch { }
                }
                
                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSize;
                
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    progress?.Report((totalRead, totalBytes));
                }
                
                return downloadPath;
            }
            catch (Exception)
            {
                // Log error or handle appropriately in release
                return null;
            }
        }
        
        /// <summary>
        /// Apply the update - creates a batch script that replaces the exe after app closes
        /// </summary>
        public static bool ApplyUpdate(string downloadedFilePath)
        {
            try
            {
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath)) return false;
                
                var currentDir = Path.GetDirectoryName(currentExePath)!;
                var currentExeName = Path.GetFileName(currentExePath);
                var backupPath = Path.Combine(currentDir, $"{currentExeName}.backup");
                
                var batchPath = Path.Combine(Path.GetTempPath(), "OsuTagUpdater.bat");
                var batchContent = $@"@echo off
chcp 65001 >nul
title osu!tag Updater

:waitloop
tasklist /FI ""PID eq {Process.GetCurrentProcess().Id}"" 2>NUL | find /I ""{Process.GetCurrentProcess().Id}"" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >NUL
    goto waitloop
)

timeout /t 1 /nobreak >NUL

if exist ""{currentExePath}"" (
    if exist ""{backupPath}"" del /f ""{backupPath}""
    move /y ""{currentExePath}"" ""{backupPath}""
)

copy /y ""{downloadedFilePath}"" ""{currentExePath}""

if exist ""{downloadedFilePath}"" del /f ""{downloadedFilePath}""
if exist ""{backupPath}"" del /f ""{backupPath}""

start """" ""{currentExePath}""

(goto) 2>nul & del ""%~f0""
";
                
                File.WriteAllText(batchPath, batchContent);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                Process.Start(startInfo);
                return true;
            }
            catch (Exception)
            {
                // Log error or handle appropriately in release
                return false;
            }
        }
        
        public static void OpenDownloadPage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        }
        
        private static string NormalizeVersion(string version) => version.TrimStart('v', 'V').Trim();
        
        private static int CompareVersions(string v1, string v2)
        {
            try
            {
                var version1 = new Version(NormalizeVersionForParsing(v1));
                var version2 = new Version(NormalizeVersionForParsing(v2));
                return version1.CompareTo(version2);
            }
            catch { return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase); }
        }
        
        private static string NormalizeVersionForParsing(string version)
        {
            var normalized = NormalizeVersion(version);
            var parts = normalized.Split('.');
            if (parts.Length == 1) return $"{normalized}.0.0";
            if (parts.Length == 2) return $"{normalized}.0";
            return normalized;
        }
    }
    
    public static class AppVersion
    {
        public const string Current = "1.1.0";
        public static string Display => $"v{Current}";
    }
}
