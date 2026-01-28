using System;
using System.Collections.Generic;
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
            var clientId = SettingsService.Settings.SpotifyClientId;
            var clientSecret = SettingsService.Settings.SpotifyClientSecret;

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return null;

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
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<SpotifyAuthResponse>(json);

                if (authResponse != null)
                {
                    _accessToken = authResponse.AccessToken;
                    _tokenExpiry = DateTime.Now.AddSeconds(authResponse.ExpiresIn - 60);
                    return _accessToken;
                }
            }
            catch
            {
                // Log error if needed
            }

            return null;
        }

        public async Task<(bool isOnSpotify, string? url)> SearchTrackAsync(string artist, string title)
        {
            var token = await GetAccessTokenAsync();
            if (token == null) return (false, null);

            // 1. Initial cleaning (always applied)
            string cleanTitle = CleanTitle(title);
            string cleanArtist = CleanArtist(artist);

            // 2. Try with conditions (ported from osu-find-songs)
            var results = await TrySearchWithConditions(cleanArtist, cleanTitle, token);
            if (results != null && results.Any())
            {
                return (true, results.First().ExternalUrls.Spotify);
            }

            return (false, null);
        }

        private async Task<List<SpotifyTrack>?> TrySearchWithConditions(string artist, string title, string token)
        {
            // List of transformations to try
            var searchQueries = new List<string>
            {
                $"artist:{artist} track:{title}",
                $"artist:{artist} track:{RemoveParentheses(title)}",
                $"artist:{artist} track:{RemoveBrackets(title)}",
                $"artist:{RemoveFeat(artist)} track:{title}",
                $"artist:{RemoveFt(artist)} track:{title}",
                $"{artist} - {title}", // Hard fallback
                $"{artist} {title}"    // Even harder fallback
            };

            foreach (var query in searchQueries.Distinct())
            {
                var results = await ExecuteSearch(query, token);
                if (results != null && results.Any()) return results;
            }

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
                var searchResponse = JsonSerializer.Deserialize<SpotifySearchResponse>(json);

                return searchResponse?.Tracks?.Items;
            }
            catch
            {
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

        private class SpotifyAuthResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = "";
            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }

        private class SpotifySearchResponse
        {
            [JsonPropertyName("tracks")]
            public SpotifyTracksContainer? Tracks { get; set; }
        }

        private class SpotifyTracksContainer
        {
            [JsonPropertyName("items")]
            public List<SpotifyTrack>? Items { get; set; }
        }

        private class SpotifyTrack
        {
            [JsonPropertyName("external_urls")]
            public SpotifyExternalUrls ExternalUrls { get; set; } = new();
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";
        }

        private class SpotifyExternalUrls
        {
            [JsonPropertyName("spotify")]
            public string Spotify { get; set; } = "";
        }
    }
}
