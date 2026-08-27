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

}
