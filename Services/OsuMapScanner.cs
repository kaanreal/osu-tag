using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OsuTag.Models;

namespace OsuTag.Services
{
    public class OsuMapScanner
    {
        /// <summary>
        /// Scans all beatmap folders in the specified directory in parallel.
        /// </summary>
        public IEnumerable<OsuMap> ScanMaps(string songsDir)
        {
            if (!Directory.Exists(songsDir))
                return Enumerable.Empty<OsuMap>();

            var folders = Directory.GetDirectories(songsDir);
            var maps = new ConcurrentBag<OsuMap>();

            Parallel.ForEach(folders, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, folder =>
            {
                var map = ScanSingleFolder(folder);
                if (map != null)
                    maps.Add(map);
            });

            return maps;
        }

        /// <summary>
        /// Scans a single beatmap folder and returns the map data, or null if invalid.
        /// </summary>
        public OsuMap? ScanSingleFolder(string folder)
        {
            try
            {
                // Use EnumerateFiles for faster first-match
                var firstOsuFile = Directory.EnumerateFiles(folder, "*.osu").FirstOrDefault();
                if (firstOsuFile == null)
                    return null;

                var metadata = ParseOsuFile(firstOsuFile);
                if (metadata == null)
                    return null;

                // Get mp3 file early - if none exists, skip folder
                var firstMp3 = Directory.EnumerateFiles(folder, "*.mp3").FirstOrDefault();
                if (firstMp3 == null)
                    return null;

                int previewTime = -1;
                if (metadata.TryGetValue("PreviewTime", out var previewStr) && 
                    int.TryParse(previewStr, out int parsedTime))
                {
                    previewTime = parsedTime;
                }

                string? coverPath = null;
                
                // First try to get the background image from the .osu file
                if (metadata.TryGetValue("BackgroundFile", out var bgFile) && !string.IsNullOrEmpty(bgFile))
                {
                    string bgPath = Path.Combine(folder, bgFile);
                    if (File.Exists(bgPath))
                        coverPath = bgPath;
                }
                
                // Fallback to finding any image in the folder
                if (coverPath == null)
                    coverPath = FindCoverImage(folder);

                var osuFiles = Directory.GetFiles(folder, "*.osu");
                var mp3Files = Directory.GetFiles(folder, "*.mp3");

                var difficulties = new List<OsuMapDifficulty>(osuFiles.Length);
                
                foreach (var osuFile in osuFiles)
                {
                    var diffMetadata = osuFile == firstOsuFile ? metadata : ParseOsuFile(osuFile);
                    if (diffMetadata == null)
                        continue;

                    string mp3Path = firstMp3;
                    if (diffMetadata.TryGetValue("AudioFilename", out var audioFile) && 
                        !string.IsNullOrEmpty(audioFile))
                    {
                        string potentialMp3 = Path.Combine(folder, audioFile);
                        if (File.Exists(potentialMp3))
                            mp3Path = potentialMp3;
                    }
                    
                    string diffName = Path.GetFileNameWithoutExtension(osuFile);
                    difficulties.Add(new OsuMapDifficulty
                    {
                        DifficultyName = diffName,
                        OsuFilePath = osuFile,
                        Mp3Path = mp3Path,
                        Rate = ExtractRate(diffName)
                    });
                }

                if (difficulties.Count == 0)
                    return null;

                return new OsuMap
                {
                    Artist = metadata.GetValueOrDefault("Artist") ?? "Unknown",
                    Title = metadata.GetValueOrDefault("Title") ?? "Unknown",
                    Creator = metadata.GetValueOrDefault("Creator") ?? "Unknown",
                    Source = metadata.GetValueOrDefault("Source"),
                    Tags = metadata.GetValueOrDefault("Tags"),
                    CoverPath = coverPath,
                    Difficulties = difficulties,
                    PreviewTime = previewTime
                };
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractRate(string difficultyName)
        {
            var match = Regex.Match(difficultyName, @"(\d+\.?\d*)\s*x", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value + "x" : null;
        }

        private static Dictionary<string, string?>? ParseOsuFile(string filePath)
        {
            try
            {
                var metadata = new Dictionary<string, string?>();
                bool inMetadataSection = false;
                bool inGeneralSection = false;
                bool inEventsSection = false;
                bool foundBackground = false;
                bool foundAllMetadata = false;

                using var reader = new StreamReader(filePath, Encoding.UTF8);
                string? line;
                
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmedLine = line.Trim();
                    
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//"))
                        continue;
                    
                    if (trimmedLine == "[Metadata]")
                    {
                        inMetadataSection = true;
                        inGeneralSection = false;
                        inEventsSection = false;
                        continue;
                    }
                    
                    if (trimmedLine == "[General]")
                    {
                        inGeneralSection = true;
                        inMetadataSection = false;
                        inEventsSection = false;
                        continue;
                    }

                    if (trimmedLine == "[Events]")
                    {
                        inEventsSection = true;
                        inMetadataSection = false;
                        inGeneralSection = false;
                        continue;
                    }

                    // Stop parsing after Events section if we have everything
                    if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
                    {
                        if (foundBackground && foundAllMetadata)
                            break;
                            
                        inMetadataSection = false;
                        inGeneralSection = false;
                        inEventsSection = false;
                        continue;
                    }

                    if ((inMetadataSection || inGeneralSection) && trimmedLine.Contains(':'))
                    {
                        var colonIndex = trimmedLine.IndexOf(':');
                        var key = trimmedLine[..colonIndex].Trim();
                        var value = trimmedLine[(colonIndex + 1)..].Trim();
                        metadata[key] = value;
                        
                        // Check if we have all needed metadata
                        if (metadata.ContainsKey("Artist") && metadata.ContainsKey("Title") && 
                            metadata.ContainsKey("Creator") && metadata.ContainsKey("AudioFilename") &&
                            metadata.ContainsKey("PreviewTime"))
                        {
                            foundAllMetadata = true;
                        }
                    }

                    // Parse background image from Events section
                    // Format: 0,0,"background.jpg",0,0
                    if (inEventsSection && !foundBackground && trimmedLine.StartsWith("0,0,"))
                    {
                        var parts = trimmedLine.Split(',');
                        if (parts.Length >= 3)
                        {
                            var bgFile = parts[2].Trim('"');
                            if (!string.IsNullOrEmpty(bgFile) && 
                                (bgFile.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                 bgFile.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                 bgFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                            {
                                metadata["BackgroundFile"] = bgFile;
                                foundBackground = true;
                            }
                        }
                    }
                }

                return metadata;
            }
            catch
            {
                return null;
            }
        }

        private static string? FindCoverImage(string folder)
        {
            // Use EnumerateFiles for faster first-match
            var image = Directory.EnumerateFiles(folder, "*.jpg").FirstOrDefault()
                     ?? Directory.EnumerateFiles(folder, "*.jpeg").FirstOrDefault()
                     ?? Directory.EnumerateFiles(folder, "*.png").FirstOrDefault();
            return image;
        }
    }
}
