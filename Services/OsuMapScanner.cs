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
                var folderMaps = ScanSingleFolder(folder);
                foreach (var map in folderMaps)
                {
                    maps.Add(map);
                }
            });

            return maps;
        }

        /// <summary>
        /// Scans a single beatmap folder and returns the map data.
        /// Returns multiple maps if it's a practice pack with different audio files.
        /// </summary>
        public IEnumerable<OsuMap> ScanSingleFolder(string folder)
        {
            var results = new List<OsuMap>();
            
            try
            {
                var osuFiles = Directory.GetFiles(folder, "*.osu");
                if (osuFiles.Length == 0)
                    return results;

                // Parse all .osu files and group by audio file
                var diffsByAudio = new Dictionary<string, List<(string osuFile, Dictionary<string, string?> metadata)>>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var osuFile in osuFiles)
                {
                    var metadata = ParseOsuFile(osuFile);
                    if (metadata == null)
                        continue;
                    
                    // Get audio filename for this difficulty
                    string audioKey = "default";
                    if (metadata.TryGetValue("AudioFilename", out var audioFile) && !string.IsNullOrEmpty(audioFile))
                    {
                        string potentialMp3 = Path.Combine(folder, audioFile);
                        if (File.Exists(potentialMp3))
                        {
                            audioKey = audioFile.ToLowerInvariant();
                        }
                    }
                    
                    if (!diffsByAudio.ContainsKey(audioKey))
                        diffsByAudio[audioKey] = new List<(string, Dictionary<string, string?>)>();
                    
                    diffsByAudio[audioKey].Add((osuFile, metadata));
                }
                
                if (diffsByAudio.Count == 0)
                    return results;
                
                // Check if this is a practice pack (multiple unique audio files)
                bool isPracticePack = diffsByAudio.Count > 1;
                
                if (isPracticePack)
                {
                    // Create separate OsuMap for each unique audio file
                    foreach (var (audioKey, diffs) in diffsByAudio)
                    {
                        var map = CreateMapFromDifficulties(folder, diffs, audioKey);
                        if (map != null)
                            results.Add(map);
                    }
                }
                else
                {
                    // Normal map - single audio, use first .osu's metadata for the group
                    var allDiffs = diffsByAudio.Values.First();
                    var map = CreateMapFromDifficulties(folder, allDiffs, diffsByAudio.Keys.First());
                    if (map != null)
                        results.Add(map);
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return results;
        }
        
        private OsuMap? CreateMapFromDifficulties(string folder, List<(string osuFile, Dictionary<string, string?> metadata)> diffs, string audioKey)
        {
            if (diffs.Count == 0)
                return null;
            
            // Use the first difficulty's metadata for the map info
            var primaryMetadata = diffs[0].metadata;
            
            // Get the mp3 path
            string? mp3Path = null;
            if (primaryMetadata.TryGetValue("AudioFilename", out var audioFile) && !string.IsNullOrEmpty(audioFile))
            {
                string potentialMp3 = Path.Combine(folder, audioFile);
                if (File.Exists(potentialMp3))
                    mp3Path = potentialMp3;
            }
            
            // Fallback to first mp3 in folder
            if (mp3Path == null)
            {
                mp3Path = Directory.EnumerateFiles(folder, "*.mp3").FirstOrDefault();
                if (mp3Path == null)
                    return null;
            }
            
            int previewTime = -1;
            if (primaryMetadata.TryGetValue("PreviewTime", out var previewStr) && 
                int.TryParse(previewStr, out int parsedTime))
            {
                previewTime = parsedTime;
            }

            string? coverPath = null;
            
            // Try to get the background image from the .osu file
            if (primaryMetadata.TryGetValue("BackgroundFile", out var bgFile) && !string.IsNullOrEmpty(bgFile))
            {
                string bgPath = Path.Combine(folder, bgFile);
                if (File.Exists(bgPath))
                    coverPath = bgPath;
            }
            
            // Fallback to finding any image in the folder
            if (coverPath == null)
                coverPath = FindCoverImage(folder);

            var difficulties = new List<OsuMapDifficulty>(diffs.Count);
            
            foreach (var (osuFile, diffMetadata) in diffs)
            {
                string diffMp3Path = mp3Path;
                if (diffMetadata.TryGetValue("AudioFilename", out var diffAudioFile) && 
                    !string.IsNullOrEmpty(diffAudioFile))
                {
                    string potentialMp3 = Path.Combine(folder, diffAudioFile);
                    if (File.Exists(potentialMp3))
                        diffMp3Path = potentialMp3;
                }
                
                string diffName = Path.GetFileNameWithoutExtension(osuFile);
                difficulties.Add(new OsuMapDifficulty
                {
                    DifficultyName = diffName,
                    OsuFilePath = osuFile,
                    Mp3Path = diffMp3Path,
                    Rate = ExtractRate(diffName)
                });
            }

            if (difficulties.Count == 0)
                return null;

            return new OsuMap
            {
                Artist = primaryMetadata.GetValueOrDefault("Artist") ?? "Unknown",
                Title = primaryMetadata.GetValueOrDefault("Title") ?? "Unknown",
                Creator = primaryMetadata.GetValueOrDefault("Creator") ?? "Unknown",
                Source = primaryMetadata.GetValueOrDefault("Source"),
                Tags = primaryMetadata.GetValueOrDefault("Tags"),
                CoverPath = coverPath,
                Difficulties = difficulties,
                PreviewTime = previewTime
            };
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
