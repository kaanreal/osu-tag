using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Reflection;
using System.Threading.Tasks;
using OsuTag.Models;

namespace OsuTag.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string Changelog { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public DateTime ReleaseDate { get; set; }
        public bool IsNewer { get; set; }
    }

    public class UpdateService
    {
        private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
        public static UpdateService Instance => _instance.Value;
        
        public bool IsUpdateAvailable { get; private set; }
        public string LatestVersion { get; private set; } = "";
        
        // Event to notify view model when update is found
        public event EventHandler? UpdateAvailable;

        private readonly HttpClient _httpClient;
        private const string GITHUB_API_URL = "https://api.github.com/repos/kaanreal/osu-tag/releases/latest";

        private UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OsuTag-Updater");
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
#if DEBUG
                // Keep the mock for debugging stability if API fails, or remove for production logic testing
                // Uncomment to force real check in debug:
#endif
                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var tagName = root.GetProperty("tag_name").GetString() ?? "";
                var body = root.GetProperty("body").GetString() ?? "";
                var publishedAt = root.GetProperty("published_at").GetDateTime();
                
                string downloadUrl = "";
                if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                {
                    // Find first suitable asset (zip/exe)
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var url = asset.GetProperty("browser_download_url").GetString();
                        if (url != null && (url.EndsWith(".exe") || url.EndsWith(".zip") || url.EndsWith(".msi")))
                        {
                            downloadUrl = url;
                            break;
                        }
                    }
                    
                    // Fallback to first asset if no specific extension match
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        downloadUrl = assets[0].GetProperty("browser_download_url").GetString() ?? "";
                    }
                }

                // Clean version string (remove 'v' prefix)
                var cleanTag = tagName.TrimStart('v');
                var currentVersion = AppVersion.Current; 
                
                // Compare versions
                // Using simple string comparison or parsing version if strictly semver
                // Assuming format x.y.z
                
                bool isNewer = false;
                if (Version.TryParse(cleanTag, out var remoteVer) && Version.TryParse(currentVersion, out var localVer))
                {
                    isNewer = remoteVer > localVer;
                }
                else
                {
                    // Fallback string compare
                     isNewer = string.Compare(cleanTag, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
                }

                // Update Service State
                IsUpdateAvailable = isNewer;
                LatestVersion = tagName;
                if (isNewer)
                {
                    UpdateAvailable?.Invoke(this, EventArgs.Empty);
                }

                return new UpdateInfo
                {
                    Version = tagName,
                    Changelog = body,
                    DownloadUrl = downloadUrl,
                    ReleaseDate = publishedAt,
                    IsNewer = isNewer
                };
            }
            catch (Exception)
            {
                // Fallback to mock for testing flow if API fails (optional, good for debugging)
#if DEBUG
                return new UpdateInfo
                {
                    Version = "v1.5.0",
                    Changelog = "Release fetch failed. This is a fallback mock.\n\n- Real GitHub API check failed.\n- Check internet connection.",
                    DownloadUrl = "https://example.com/mock.zip",
                    IsNewer = true
                };
#else
                return null;
#endif
            }
        }

        public async Task DownloadUpdateAsync(string downloadUrl, IProgress<double> progress)
        {
            try
            {
                // Download to temp file
                var tempPath = Path.Combine(Path.GetTempPath(), "osutag_update_v" + DateTime.Now.Ticks + ".exe");
                
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var totalRead = 0L;
                        var buffer = new byte[8192];
                        var isMoreToRead = true;

                        do
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, read);

                                totalRead += read;
                                if (canReportProgress)
                                {
                                    progress.Report((double)totalRead / totalBytes * 100);
                                }
                            }
                        }
                        while (isMoreToRead);
                    }
                }
                
                // Set path for next step (Apply)
                _downloadedFilePath = tempPath;
            }
            catch (Exception)
            {
                 throw;
            }
        }

        private string? _downloadedFilePath;

        public void ApplyUpdate()
        {
            if (string.IsNullOrEmpty(_downloadedFilePath) || !File.Exists(_downloadedFilePath))
            {
                return;
            }

            // Launch the new installer/exe
            // If it's an installer, run it. If it's a raw exe replacement, we need a script.
            // Assuming for now the update is a setup.exe or self-extracting archive
            try 
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _downloadedFilePath,
                    UseShellExecute = true 
                });
                
                Environment.Exit(0);
            }
            catch (Exception)
            {
            }
        }

        public void IgnoreUpdate(string version)
        {
            Console.WriteLine($"Ignored version: {version}");
        }
    }
}
