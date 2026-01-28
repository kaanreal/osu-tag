using System;
using System.Collections.Generic;

namespace Osutag.Models
{
    public class OsuMapDifficulty
    {
        public required string DifficultyName { get; set; }
        public required string OsuFilePath { get; set; }
        public required string Mp3Path { get; set; }
        public string? Rate { get; set; }
        
        // Detailed metadata for overlay
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? CoverPath { get; set; }
        public int PreviewTime { get; set; } = -1;
        public bool IsOnSpotify { get; set; }
        public string? SpotifyUrl { get; set; }
    }

    public class OsuMap
    {
        public required string Artist { get; set; }
        public required string Title { get; set; }
        public required string Creator { get; set; }
        public string? Source { get; set; }
        public string? Tags { get; set; }
        public string? CoverPath { get; set; }
        public int BeatmapSetId { get; set; } = -1;
        public required List<OsuMapDifficulty> Difficulties { get; set; } = new();
        public int PreviewTime { get; set; } = -1;
        public bool IsOnSpotify { get; set; }
        public string? SpotifyUrl { get; set; }
    }
}
