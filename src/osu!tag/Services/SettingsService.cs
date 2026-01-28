using System;
using System.IO;
using System.Text.Json;

namespace Osutag.Services
{
    /// <summary>
    /// Cross-platform settings service using JSON file storage.
    /// Replaces WPF's Properties.Settings.Default.
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsFilePath;
        private static AppSettings _settings = new();
        private static readonly object _lock = new();

        static SettingsService()
        {
            // Store settings in user's app data folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsDir = Path.Combine(appDataPath, "osu!tag");
            Directory.CreateDirectory(settingsDir);
            SettingsFilePath = Path.Combine(settingsDir, "settings.json");
            
            Load();
        }

        public static AppSettings Settings => _settings;

        public static void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        var json = File.ReadAllText(SettingsFilePath);
                        _settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings) ?? new AppSettings();
                    }
                }
                catch
                {
                    _settings = new AppSettings();
                }
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_settings, AppJsonContext.Default.AppSettings);
                    File.WriteAllText(SettingsFilePath, json);
                }
                catch
                {
                    // Silently fail - settings are not critical
                }
            }
        }
    }

    public class AppSettings
    {
        public bool ProcessCovers { get; set; } = true;
        public bool CreateBackups { get; set; } = false;
        public string FileNameFormat { get; set; } = "{artist} - {title} ({difficulty})";
        public double PreviewVolume { get; set; } = 30;
        public string ThemeColor { get; set; } = "#5B9FED";
        public string LastUsedPath { get; set; } = "";
        public bool RememberSongsPath { get; set; } = true;
        public string LastSongsPath { get; set; } = "";
        public bool SmartScan { get; set; } = true;
        public string ScannedFolders { get; set; } = "";
        public bool SortByMostPlayed { get; set; } = true;
        public string CompanellaPath { get; set; } = "";
        public bool CheckForUpdates { get; set; } = true;
        public string SkipUpdateVersion { get; set; } = "";
        public bool TelemetryEnabled { get; set; } = true;
        public bool DiscordRpcEnabled { get; set; } = true;
        public string AnonymousUserId { get; set; } = "";
        public string SpotifyClientId { get; set; } = "";
        public string SpotifyClientSecret { get; set; } = "";
        public bool DynamicBackgroundEnabled { get; set; } = false;
        public string OsuPath { get; set; } = "";
    }
}
