using System.Collections.Generic;
using System.Text.Json.Serialization;
using Osutag.Services;
using Osutag.ViewModels;

namespace Osutag
{
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(List<MainViewModel.CachedMapData>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(AptabasePayload))]
    [JsonSerializable(typeof(SpotifyAuthResponse))]
    [JsonSerializable(typeof(SpotifySearchResponse))]
    [JsonSerializable(typeof(SpotifyTracksContainer))]
    [JsonSerializable(typeof(SpotifyTrack))]
    [JsonSerializable(typeof(SpotifyExternalUrls))]
    [JsonSerializable(typeof(SpotifyConfig))]
    [JsonSerializable(typeof(SpotifySearchResult))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }

    public class AptabasePayload
    {
        [JsonPropertyName("EventName")]
        public string EventName { get; set; } = "";
        
        [JsonPropertyName("SessionId")]
        public string? SessionId { get; set; }
        
        [JsonPropertyName("Timestamp")]
        public string Timestamp { get; set; } = "";
        
        [JsonPropertyName("SystemProps")]
        public Dictionary<string, object>? SystemProps { get; set; }
        
        [JsonPropertyName("Props")]
        public Dictionary<string, object>? Props { get; set; }
    }

    // Spotify API response types
    public class SpotifyAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class SpotifySearchResponse
    {
        [JsonPropertyName("tracks")]
        public SpotifyTracksContainer? Tracks { get; set; }
    }

    public class SpotifyTracksContainer
    {
        [JsonPropertyName("items")]
        public List<SpotifyTrack>? Items { get; set; }
    }

    public class SpotifyTrack
    {
        [JsonPropertyName("external_urls")]
        public SpotifyExternalUrls ExternalUrls { get; set; } = new();
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    public class SpotifyExternalUrls
    {
        [JsonPropertyName("spotify")]
        public string Spotify { get; set; } = "";
    }

    // Spotify configuration for local development
    public class SpotifyConfig
    {
        [JsonPropertyName("SpotifyClientId")]
        public string? SpotifyClientId { get; set; }
        
        [JsonPropertyName("SpotifyClientSecret")]
        public string? SpotifyClientSecret { get; set; }
    }

    // Spotify search result from backend API
    public class SpotifySearchResult
    {
        [JsonPropertyName("found")]
        public bool Found { get; set; }
        
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
