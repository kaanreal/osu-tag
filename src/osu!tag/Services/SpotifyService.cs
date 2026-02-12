using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Osutag.Models;

namespace Osutag.Services
{
    /// <summary>
    /// Spotify integration service for detecting which osu! songs are available on Spotify.
    /// Search logic adapted from osu-find-songs: https://github.com/kaanreal/osu-find-songs
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute'", Justification = "All types used in deserialization are defined locally and preserved.")]
    public class SpotifyService
    {
        private static SpotifyService? _instance;
        public static SpotifyService Instance => _instance ??= new SpotifyService();

        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private SpotifyService()
        {
            _httpClient = new HttpClient();
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                System.Diagnostics.Debug.WriteLine("[Spotify] Credentials not configured. Set SPOTIFY_CLIENT_ID and SPOTIFY_CLIENT_SECRET environment variables.");
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
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track&limit=1");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize(json, AppJsonContext.Default.SpotifySearchResponse);

                return searchResponse?.Tracks?.Items;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Spotify] Search exception: {ex.Message}");
                return null;
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
