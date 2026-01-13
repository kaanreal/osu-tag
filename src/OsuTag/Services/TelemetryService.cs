using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using System.Runtime.InteropServices;
using System.IO; 

namespace OsuTag.Services
{
    public static class TelemetryService
    {
        // ============================================
        // APTABASE CONFIGURATION
        // ============================================
        // App Key: This identifies your app (format: A-XX-XXXXXXXXXX)
        private const string AptabaseAppKey = "A-EU-8378867321";
        
        // Endpoint: Your self-hosted Aptabase server URL
        // Note: Use http:// for localhost to avoid SSL certificate issues
        private const string AptabaseEndpoint = "https://eu.aptabase.com/";
        // ============================================
        
        private static HttpClient? _httpClient;
        private static bool _initialized = false;
        private static string? _sessionId;
        private static DateTime? _sessionStartTime;
        
        /// <summary>
        /// Initialize the telemetry service (call once at app startup)
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            // Create HttpClientHandler
            var handler = new HttpClientHandler();
            
            // Bypass SSL validation for HTTPS localhost endpoints (if needed)
            if (AptabaseEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                (AptabaseEndpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase) || 
                 AptabaseEndpoint.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
            {
                handler.ServerCertificateCustomValidationCallback = 
                    (httpRequestMessage, cert, cetChain, policyErrors) => true;
            }
            
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("App-Key", AptabaseAppKey);
            // Note: Content-Type is set automatically by StringContent, don't add it to headers
            _initialized = true;
        }

        private static string GetOsPlatform()
        {
            return PlatformService.GetPlatformName();
        }
        
        /// <summary>
        /// Track an event if telemetry is enabled
        /// </summary>
        public static async Task TrackEventAsync(string eventName, Dictionary<string, object>? props = null)
        {
            // Check if telemetry is enabled
            if (!SettingsService.Settings.TelemetryEnabled)
            {
#if DEBUG
                try
                {
                    var temp = Path.Combine(Path.GetTempPath(), "aptabase_debug.log");
                    File.AppendAllText(temp, $"Telemetry disabled - skipping event: {eventName}\n");
                    System.Diagnostics.Debug.WriteLine($"Telemetry disabled - skipping event: {eventName}");
                }
                catch { }
#endif
                return;
            }
            
            // Validate configuration - skip if still using placeholder values
            if (string.IsNullOrWhiteSpace(AptabaseAppKey) || AptabaseAppKey == "A-EU-0000000000" ||
                string.IsNullOrWhiteSpace(AptabaseEndpoint) || AptabaseEndpoint == "https://your-aptabase-server.com")
                return;
            
            try
            {
                Initialize();
                
                // Generate session ID if not already created (persists for app lifetime)
                if (_sessionId == null)
                {
                    _sessionId = Guid.NewGuid().ToString();
                }

#if DEBUG
                try
                {
                    var temp = Path.Combine(Path.GetTempPath(), "aptabase_debug.log");
                    File.AppendAllText(temp, $"Sending event: {eventName}, SessionId: {_sessionId}, Endpoint: {AptabaseEndpoint}\n");
                    System.Diagnostics.Debug.WriteLine($"Sending event: {eventName}, SessionId: {_sessionId}, Endpoint: {AptabaseEndpoint}");
                }
                catch { }
#endif
                
                // Build custom properties (sorted for consistent display)
                var customProps = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (props != null)
                {
                    foreach (var kvp in props)
                    {
                        customProps[kvp.Key] = kvp.Value;
                    }
                }
                
                // Ensure canonical, display-friendly keys are present
                customProps["version"] = AppVersion.Current;
                customProps["app_version"] = AppVersion.Current;
                customProps["os"] = GetOsPlatform();
                customProps["os_description"] = RuntimeInformation.OSDescription;
                customProps["os_version"] = Environment.OSVersion.Version.ToString();
                customProps["os_architecture"] = RuntimeInformation.OSArchitecture.ToString();
                customProps["process_architecture"] = RuntimeInformation.ProcessArchitecture.ToString();
                customProps["framework"] = RuntimeInformation.FrameworkDescription;
                customProps["is_64bit_os"] = Environment.Is64BitOperatingSystem;
                customProps["is_64bit_process"] = Environment.Is64BitProcess;
                customProps["culture"] = CultureInfo.CurrentCulture.Name;
                customProps["timezone"] = TimeZoneInfo.Local.Id;

                // COMMON ALIASES FOR UI COMPATIBILITY (don't overwrite if already provided)
                if (!customProps.ContainsKey("OS")) customProps["OS"] = customProps["os"];
                if (!customProps.ContainsKey("operating_system")) customProps["operating_system"] = customProps["os"];
                if (!customProps.ContainsKey("platform")) customProps["platform"] = customProps["os"];

                if (!customProps.ContainsKey("Version")) customProps["Version"] = customProps["version"];
                if (!customProps.ContainsKey("appVersion")) customProps["appVersion"] = customProps["app_version"];
                if (!customProps.ContainsKey("App Version")) customProps["App Version"] = customProps["app_version"];

                // Minimal payload: only SystemProps Aptabase session UI reads
                string country = "unknown";
                try
                {
                    var region = new System.Globalization.RegionInfo(CultureInfo.CurrentCulture.Name);
                    country = region.EnglishName;
                }
                catch
                {
                    // ignore - leave as unknown
                }

#if DEBUG
                var isDebug = true;
#else
                var isDebug = false;
#endif

                // Try to provide both English name and ISO country code (flags often require ISO2 code)
                string countryCode = "";
                try
                {
                    var region = new System.Globalization.RegionInfo(CultureInfo.CurrentCulture.Name);
                    countryCode = region.TwoLetterISORegionName; // e.g. "US", "DE"
                }
                catch
                {
                    countryCode = string.Empty;
                }

                // Use canonical camelCase keys that Aptabase's API/UI expects
                // Prefer lowercase ISO2 country code in the canonical `country` field (Aptabase commonly expects lowercase)
                var countryCodeUpper = string.IsNullOrWhiteSpace(countryCode) ? string.Empty : countryCode.ToUpperInvariant();
                var countryCodeLower = string.IsNullOrWhiteSpace(countryCode) ? string.Empty : countryCode.ToLowerInvariant();
                var countryForSystem = !string.IsNullOrWhiteSpace(countryCodeLower) ? countryCodeLower : country;

                var systemProps = new Dictionary<string, object>
                {
                    ["sdkVersion"] = $"osu-tag/{AppVersion.Current ?? "unknown"}",
                    ["isDebug"] = isDebug,

                    // App/version
                    ["appVersion"] = AppVersion.Current ?? "unknown",
                    ["appBuildNumber"] = AppVersion.Current ?? "unknown",

                    // OS
                    ["osName"] = GetOsPlatform(),
                    ["osVersion"] = Environment.OSVersion.Version.ToString(),
                    ["osPlatform"] = GetOsPlatform(),

                    // Locale / country - `country` is lowercase ISO2 (e.g., "us"); `countryName` stores English name
                    ["locale"] = CultureInfo.CurrentCulture.Name,
                    ["country"] = countryForSystem,
                    ["countryCode"] = countryCodeUpper,
                    ["country_code"] = countryCodeUpper,
                    ["country_iso"] = countryCodeUpper,
                    ["countryName"] = country,
                    ["country_name"] = country,
                    ["Country"] = country
                };

                var payload = new AptabasePayload
                {
                    EventName = eventName,
                    SessionId = _sessionId,
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    SystemProps = systemProps,
                    Props = props != null ? new Dictionary<string, object>(props) : new Dictionary<string, object>()
                };

                var json = JsonSerializer.Serialize(payload, AppJsonContext.Default.AptabasePayload);

#if DEBUG
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Aptabase payload: {json}");
                    var tempPath = Path.Combine(Path.GetTempPath(), "aptabase_last_payload.json");
                    File.WriteAllText(tempPath, json);
                }
                catch
                {
                    // ignore logging errors
                }
#endif

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var baseUrl = AptabaseEndpoint.TrimEnd('/');
                var url = $"{baseUrl}/api/v0/event";

                if (_httpClient == null)
                {
#if DEBUG
                    try
                    {
                        var temp = Path.Combine(Path.GetTempPath(), "aptabase_debug.log");
                        File.AppendAllText(temp, $"HttpClient is null - cannot send event: {eventName}\n");
                        System.Diagnostics.Debug.WriteLine($"HttpClient is null - cannot send event: {eventName}");
                    }
                    catch { }
#endif
                }
                else
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

                        // Add Cloudflare / client country headers to help self-hosted Aptabase identify country
                        if (!string.IsNullOrWhiteSpace(countryCodeUpper))
                        {
                            request.Headers.Remove("CF-IPCountry");
                            request.Headers.Remove("X-Client-Country");
                            request.Headers.Add("CF-IPCountry", countryCodeUpper);
                            request.Headers.Add("X-Client-Country", countryCodeUpper);
                        }

#if DEBUG
                        try
                        {
                            var temp = Path.Combine(Path.GetTempPath(), "aptabase_debug.log");
                            File.AppendAllText(temp, $"Sending headers CF-IPCountry={countryCodeUpper}\n");
                            System.Diagnostics.Debug.WriteLine($"Sending headers CF-IPCountry={countryCodeUpper}");
                        }
                        catch { }
#endif

                        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#if DEBUG
                            try
                            {
                                var temp = Path.Combine(Path.GetTempPath(), "aptabase_error.log");
                                File.AppendAllText(temp, $"Aptabase error for event {eventName}: {response.StatusCode} - {responseBody}\n");
                                System.Diagnostics.Debug.WriteLine($"Aptabase error: {response.StatusCode} - {responseBody}");
                            }
                            catch { }
#endif
                        }
                        else
                        {
#if DEBUG
                            try
                            {
                                var temp = Path.Combine(Path.GetTempPath(), "aptabase_debug.log");
                                File.AppendAllText(temp, $"Event sent successfully: {eventName}\n");
                                System.Diagnostics.Debug.WriteLine($"✓ Aptabase event sent: {eventName}");
                            }
                            catch { }
#endif
                        }
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        try
                        {
                            var temp = Path.Combine(Path.GetTempPath(), "aptabase_error.log");
                            File.AppendAllText(temp, $"Exception sending event {eventName}: {ex.Message}\n");
                            System.Diagnostics.Debug.WriteLine($"Aptabase post exception: {ex.Message}");
                        }
                        catch { }
#else
                        _ = ex; // Avoid warning in Release
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                #if DEBUG
                var errorMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMsg += $" (Inner: {ex.InnerException.Message})";
                }
                System.Diagnostics.Debug.WriteLine($"Aptabase telemetry error: {errorMsg}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                #else
                _ = ex; // Avoid warning in Release
                #endif
                // Silently fail - telemetry should never break the app
            }
        }
        
        /// <summary>
        /// Track app launch
        /// </summary>
        public static async Task TrackAppLaunch()
        {
            // Start a session and ensure session_start is processed before app_launched
            _sessionStartTime = DateTime.UtcNow;
            await TrackEventAsync("session_start");
            await TrackEventAsync("app_launched");
        }

        /// <summary>
        /// Mark the start of a session (useful for duration calculation)
        /// </summary>
        public static Task TrackSessionStart()
        {
            _sessionStartTime = DateTime.UtcNow;
            return TrackEventAsync("session_start");
        }

        /// <summary>
        /// Mark the end of a session and send duration (seconds)
        /// </summary>
        public static Task TrackSessionStop()
        {
            double durationSeconds = 0;
            if (_sessionStartTime.HasValue)
            {
                durationSeconds = Math.Round((DateTime.UtcNow - _sessionStartTime.Value).TotalSeconds, 2);
            }
            _sessionStartTime = null;
            return TrackEventAsync("session_stop", new Dictionary<string, object> { ["duration_seconds"] = durationSeconds });
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
        /// Track total conversions (number of songs converted)
        /// </summary>
        public static Task TrackTotalConversions(int convertedCount)
        {
            // Event name shown in Aptabase: "Total Conversions"
            return TrackEventAsync("Total Conversions", new Dictionary<string, object>
            {
                ["converted_count"] = convertedCount
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

    public static class AppVersion
    {
        public static string Current { get; } = typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
