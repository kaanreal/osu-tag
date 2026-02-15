using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Osutag.Services
{
    /// <summary>
    /// Spotify integration service for detecting which osu! songs are available on Spotify.
    /// Uses direct Spotify API with rate limiting and multiple credential sources.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute'", Justification = "All types used in deserialization are defined locally and preserved.")]
    public class SpotifyService
    {
        private static SpotifyService? _instance;
        public static SpotifyService Instance => _instance ??= new SpotifyService();

        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private DateTime _tokenExpiry;

        private string? _supabaseUrl;
        private string? _supabaseAnonKey;
        private bool _supabaseConfigLoaded;

        // Hardcoded Supabase config (publishable anon key + URL).
        // This is public by design; do not put service keys here.
        private const string HardcodedSupabaseUrl = "https://dpnfdszjftefgnosqpdc.supabase.co";
        private const string HardcodedSupabaseAnonKey = "sb_publishable_5Ray9CgUvQS0sAyLx-Ww3w_W6OeVURy";
        
        // Rate limiting
        private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinRequestDelayMs = 300; // 300ms between requests

        private SpotifyService()
        {
            _httpClient = new HttpClient();
        }

        private void EnsureSupabaseConfigLoaded()
        {
            if (_supabaseConfigLoaded) return;

            _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
            _supabaseAnonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");

            if (string.IsNullOrWhiteSpace(_supabaseUrl)) _supabaseUrl = HardcodedSupabaseUrl;
            if (string.IsNullOrWhiteSpace(_supabaseAnonKey)) _supabaseAnonKey = HardcodedSupabaseAnonKey;

            if (string.IsNullOrWhiteSpace(_supabaseUrl) || string.IsNullOrWhiteSpace(_supabaseAnonKey))
            {
                System.Diagnostics.Debug.WriteLine("[Spotify] Supabase not configured. Set SUPABASE_URL and SUPABASE_ANON_KEY or provide supabase-config.json");
            }

            _supabaseConfigLoaded = true;
        }

        private async Task<(bool isOnSpotify, string? url)?> SearchViaSupabaseAsync(string artist, string title)
        {
            EnsureSupabaseConfigLoaded();
            if (string.IsNullOrWhiteSpace(_supabaseUrl) || string.IsNullOrWhiteSpace(_supabaseAnonKey))
                return null;

            try
            {
                var endpoint = _supabaseUrl.TrimEnd('/');
                if (!endpoint.Contains("/functions/", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = $"{endpoint}/functions/v1/spotify-search";
                }
                var payload = new SupabaseSpotifySearchRequest
                {
                    Artist = artist,
                    Title = title
                };

                var json = JsonSerializer.Serialize(payload, AppJsonContext.Default.SupabaseSpotifySearchRequest);
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseAnonKey);
                request.Headers.Add("apikey", _supabaseAnonKey);

                var response = await _httpClient.SendAsync(request);
                var responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[Spotify] Supabase function failed: {response.StatusCode}, Response: {responseJson}");
                    return null;
                }

                var result = JsonSerializer.Deserialize(responseJson, AppJsonContext.Default.SpotifySearchResult);
                if (result == null) return (false, null);
                if (!result.Found || string.IsNullOrWhiteSpace(result.Url)) return (false, null);

                return (true, result.Url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Spotify] Supabase search exception: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                System.Diagnostics.Debug.WriteLine("[Spotify] Credentials not configured. Supabase is required for Spotify lookups.");
                return null;
            }

            if (_accessToken != null && DateTime.Now < _tokenExpiry)
                return _accessToken;

            try
            {
                var dict = new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", clientId },
                    { "client_secret", clientSecret }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
                {
                    Content = new FormUrlEncodedContent(dict)
                };

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[Spotify] Auth failed: {response.StatusCode}, Response: {json}");
                    return null;
                }

                var authResponse = JsonSerializer.Deserialize(json, AppJsonContext.Default.SpotifyAuthResponse);
                System.Diagnostics.Debug.WriteLine($"[Spotify] Token obtained successfully");

                if (authResponse != null)
                {
                    _accessToken = authResponse.AccessToken;
                    _tokenExpiry = DateTime.Now.AddSeconds(authResponse.ExpiresIn - 60);
                    return _accessToken;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Spotify] Auth exception: {ex.Message}");
            }

            return null;
        }

        public async Task<(bool isOnSpotify, string? url)> SearchTrackAsync(string artist, string title)
        {
            var supabaseResult = await SearchViaSupabaseAsync(artist, title);
            if (supabaseResult.HasValue)
            {
                return supabaseResult.Value;
            }

            var token = await GetAccessTokenAsync();
            if (token == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Spotify] Failed to get token for {artist} - {title}");
                return (false, null);
            }

            // 1. Initial cleaning (always applied)
            string cleanTitle = CleanTitle(title);
            string cleanArtist = CleanArtist(artist);

            // 2. Try with conditions (ported from osu-find-songs)
            var results = await TrySearchWithConditions(cleanArtist, cleanTitle, token);
            if (results != null && results.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[Spotify] ✓ Found: {artist} - {title}");
                return (true, results.First().ExternalUrls.Spotify);
            }

            System.Diagnostics.Debug.WriteLine($"[Spotify] ✗ Not found: {artist} - {title}");
            return (false, null);
        }

        private async Task<List<SpotifyTrack>?> TrySearchWithConditions(string artist, string title, string token)
        {
            // Condition 1: Try as-is first (matches osu-find-songs: (s: Song) => s)
            var result = await ExecuteSearch($"artist:{artist} track:{title}", token);
            if (result != null && result.Any()) return result;

            // Condition 2: Remove parentheses from title (if present)
            if (title.Contains('(') && title.Contains(')'))
            {
                var cleanTitle = RemoveParentheses(title);
                result = await ExecuteSearch($"artist:{artist} track:{cleanTitle}", token);
                if (result != null && result.Any()) return result;
            }

            // Condition 3: Remove brackets from title (if present)
            if (title.Contains('[') && title.Contains(']'))
            {
                var cleanTitle = RemoveBrackets(title);
                result = await ExecuteSearch($"artist:{artist} track:{cleanTitle}", token);
                if (result != null && result.Any()) return result;
            }

            // Condition 4: Remove 'feat' from artist (if present)
            if (artist.Contains("feat", StringComparison.OrdinalIgnoreCase))
            {
                var cleanArtist = RemoveFeat(artist);
                result = await ExecuteSearch($"artist:{cleanArtist} track:{title}", token);
                if (result != null && result.Any()) return result;
            }

            // Condition 5: Remove 'ft' from artist (if present)
            if (artist.Contains("ft", StringComparison.OrdinalIgnoreCase))
            {
                var cleanArtist = RemoveFt(artist);
                result = await ExecuteSearch($"artist:{cleanArtist} track:{title}", token);
                if (result != null && result.Any()) return result;
            }

            // Don't try hard conditions (title-only/artist-only) as they cause too many false positives
            return null;
        }

        private async Task<List<SpotifyTrack>?> ExecuteSearch(string query, string token)
        {
            // Rate limiting: ensure minimum delay between requests
            await _rateLimitSemaphore.WaitAsync();
            try
            {
                var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
                if (timeSinceLastRequest.TotalMilliseconds < MinRequestDelayMs)
                {
                    var delayNeeded = MinRequestDelayMs - (int)timeSinceLastRequest.TotalMilliseconds;
                    await Task.Delay(delayNeeded);
                }

                var escapedQuery = Uri.EscapeDataString(query);
                var url = $"https://api.spotify.com/v1/search?q={escapedQuery}&type=track&limit=1";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                _lastRequestTime = DateTime.Now;
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    // Check for Retry-After header
                    if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                    {
                        if (int.TryParse(retryAfterValues.First(), out var retryAfterSeconds))
                        {
                            // Cap retry time at 60 seconds to avoid extremely long waits
                            if (retryAfterSeconds > 60)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Spotify] Rate limited for {retryAfterSeconds}s. Skipping.");
                                return null;
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"[Spotify] Rate limited. Waiting {retryAfterSeconds}s...");
                            await Task.Delay(retryAfterSeconds * 1000);
                            
                            // Retry the request
                            request = new HttpRequestMessage(HttpMethod.Get, url);
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                            response = await _httpClient.SendAsync(request);
                            _lastRequestTime = DateTime.Now;
                        }
                    }
                    else
                    {
                        await Task.Delay(2000);
                        return null;
                    }
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize(json, AppJsonContext.Default.SpotifySearchResponse);

                return searchResponse?.Tracks?.Items;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Spotify] Search exception: {ex.Message}");
                return null;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        private string CleanTitle(string title)
        {
            return title.Replace("(TV Size)", "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        private string CleanArtist(string artist)
        {
            return artist.Trim();
        }

        private string RemoveParentheses(string text)
        {
            return Regex.Replace(text, @"\s*\(.*?\)\s*", "").Trim();
        }

        private string RemoveBrackets(string text)
        {
            return Regex.Replace(text, @"\s*\[.*?\]\s*", "").Trim();
        }

        private string RemoveFeat(string text)
        {
            return Regex.Replace(text, @"\s*feat.*", "", RegexOptions.IgnoreCase).Trim();
        }

        private string RemoveFt(string text)
        {
            return Regex.Replace(text, @"\s*ft.*", "", RegexOptions.IgnoreCase).Trim();
        }
    }
}
