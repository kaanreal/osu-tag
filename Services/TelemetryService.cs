using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OsuTag.Services
{
    public static class TelemetryService
    {
        // ============================================
        // APTABASE CONFIGURATION
        // ============================================
        // App Key: This identifies your app (format: A-XX-XXXXXXXXXX)
        private const string AptabaseAppKey = "A-EU-0000000000"; // Replace with your actual App Key from Aptabase
        
        // Endpoint: Your friend's self-hosted Aptabase server URL
        private const string AptabaseEndpoint = "https://your-aptabase-server.com"; // Replace with your friend's server URL
        // ============================================
        
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private static bool _initialized = false;
        
        /// <summary>
        /// Initialize the telemetry service (call once at app startup)
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("App-Key", AptabaseAppKey);
            _initialized = true;
        }
        
        /// <summary>
        /// Track an event if telemetry is enabled
        /// </summary>
        public static async Task TrackEventAsync(string eventName, Dictionary<string, object>? props = null)
        {
            // Check if telemetry is enabled
            if (!Properties.Settings.Default.TelemetryEnabled)
                return;
            
            try
            {
                Initialize();
                
                // Build properties with default app info
                var properties = new Dictionary<string, object>
                {
                    ["app_version"] = AppVersion.Current,
                    ["os_version"] = Environment.OSVersion.Version.ToString()
                };
                
                // Merge custom properties
                if (props != null)
                {
                    foreach (var kvp in props)
                    {
                        properties[kvp.Key] = kvp.Value;
                    }
                }
                
                // Aptabase event format
                var payload = new
                {
                    eventName = eventName,
                    props = properties
                };
                
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Fire and forget - don't block the app
                var url = $"{AptabaseEndpoint.TrimEnd('/')}/api/v0/event";
                await _httpClient.PostAsync(url, content).ConfigureAwait(false);
            }
            catch
            {
                // Silently fail - telemetry should never break the app
            }
        }
        
        /// <summary>
        /// Track app launch
        /// </summary>
        public static Task TrackAppLaunch()
        {
            return TrackEventAsync("app_launched");
        }
        
        /// <summary>
        /// Track beatmap scan
        /// </summary>
        public static Task TrackScan(int mapCount, double durationSeconds)
        {
            return TrackEventAsync("scan_completed", new Dictionary<string, object>
            {
                ["map_count"] = mapCount,
                ["duration_seconds"] = Math.Round(durationSeconds, 2)
            });
        }
        
        /// <summary>
        /// Track export operation
        /// </summary>
        public static Task TrackExport(int mapCount, bool withCovers, bool withRates)
        {
            return TrackEventAsync("export_completed", new Dictionary<string, object>
            {
                ["map_count"] = mapCount,
                ["with_covers"] = withCovers,
                ["with_rates"] = withRates
            });
        }
        
        /// <summary>
        /// Track feature usage
        /// </summary>
        public static Task TrackFeatureUsed(string featureName)
        {
            return TrackEventAsync("feature_used", new Dictionary<string, object>
            {
                ["feature"] = featureName
            });
        }
        
        /// <summary>
        /// Track settings changed
        /// </summary>
        public static Task TrackSettingsChanged(Dictionary<string, object> settings)
        {
            return TrackEventAsync("settings_changed", settings);
        }
        
        /// <summary>
        /// Track error (without personal data)
        /// </summary>
        public static Task TrackError(string errorType, string? context = null)
        {
            return TrackEventAsync("error_occurred", new Dictionary<string, object>
            {
                ["error_type"] = errorType,
                ["context"] = context ?? "unknown"
            });
        }
    }
}
