using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace OsuTag.Services
{
    /// <summary>
    /// Service for reading play count data from Companella's databases.
    /// Companella uses two databases:
    /// - sessions.db: Contains SessionPlays table with BeatmapPath
    /// - maps.db: Contains Maps table with metadata (Title, Artist, etc.)
    /// </summary>
    public class CompanellaService
    {
        private readonly string _sessionsDbPath;
        private readonly string _mapsDbPath;

        public CompanellaService(string companellaPath)
        {
            _sessionsDbPath = Path.Combine(companellaPath, "sessions.db");
            _mapsDbPath = Path.Combine(companellaPath, "maps.db");
        }

        /// <summary>
        /// Checks if the Companella databases exist and are accessible.
        /// </summary>
        public bool IsAvailable()
        {
            return File.Exists(_sessionsDbPath);
        }

        /// <summary>
        /// Gets the play counts for all maps from the Companella databases.
        /// Returns a dictionary mapping folder names to play counts.
        /// </summary>
        public Dictionary<string, int> GetPlayCounts()
        {
            var playCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (!IsAvailable())
                return playCounts;

            try
            {
                // Use folder-based matching for reliability
                return GetPlayCountsByFolder();
            }
            catch (Exception)
            {
                // Return empty dictionary on any error
            }

            return playCounts;
        }

        /// <summary>
        /// Gets play counts by joining sessions.db and maps.db for proper Artist/Title metadata.
        /// </summary>
        private Dictionary<string, int> GetPlayCountsWithMetadata()
        {
            var playCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var sessionsConnStr = new SqliteConnectionStringBuilder
            {
                DataSource = _sessionsDbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(sessionsConnStr);
            connection.Open();

            // Attach the maps database
            using (var attachCmd = new SqliteCommand($"ATTACH DATABASE '{_mapsDbPath.Replace("'", "''")}' AS maps", connection))
            {
                attachCmd.ExecuteNonQuery();
            }

            // Query with join to get Artist and Title from maps.db
            var query = @"
                SELECT 
                    m.Artist,
                    m.Title,
                    COUNT(sp.BeatmapPath) AS Playcount
                FROM maps.Maps m
                LEFT JOIN SessionPlays sp ON m.BeatmapPath = sp.BeatmapPath
                GROUP BY m.Artist, m.Title
                ORDER BY Playcount DESC";

            using var cmd = new SqliteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            
            while (reader.Read())
            {
                var artist = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var title = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var count = reader.GetInt32(2);
                
                if (string.IsNullOrEmpty(artist) && string.IsNullOrEmpty(title))
                    continue;
                    
                var key = $"{artist} - {title}";
                
                if (playCounts.ContainsKey(key))
                    playCounts[key] += count;
                else
                    playCounts[key] = count;
            }

            return playCounts;
        }

        /// <summary>
        /// Gets play counts grouped by folder name for reliable matching.
        /// </summary>
        private Dictionary<string, int> GetPlayCountsByFolder()
        {
            var playCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _sessionsDbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // Get play counts grouped by BeatmapPath
            var query = @"
                SELECT BeatmapPath, COUNT(*) as Playcount 
                FROM SessionPlays 
                GROUP BY BeatmapPath 
                ORDER BY Playcount DESC";

            using var cmd = new SqliteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            
            while (reader.Read())
            {
                var beatmapPath = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (string.IsNullOrEmpty(beatmapPath))
                    continue;
                    
                var count = reader.GetInt32(1);
                
                // Get folder name as key (e.g., "2179506 Long ZhixiangWu Feihua - Da Xiang Jiao")
                var folderName = GetFolderName(beatmapPath);
                if (string.IsNullOrEmpty(folderName))
                    continue;
                
                if (playCounts.ContainsKey(folderName))
                    playCounts[folderName] += count;
                else
                    playCounts[folderName] = count;
            }

            return playCounts;
        }

        /// <summary>
        /// Gets play counts from sessions.db only, extracting Artist/Title from BeatmapPath.
        /// </summary>
        private Dictionary<string, int> GetPlayCountsFromSessionsOnly()
        {
            var playCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _sessionsDbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // Get play counts grouped by BeatmapPath
            var query = @"
                SELECT BeatmapPath, COUNT(*) as Playcount 
                FROM SessionPlays 
                GROUP BY BeatmapPath 
                ORDER BY Playcount DESC";

            using var cmd = new SqliteCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            
            while (reader.Read())
            {
                var beatmapPath = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (string.IsNullOrEmpty(beatmapPath))
                    continue;
                    
                var count = reader.GetInt32(1);
                
                // Extract "Artist - Title" from the folder name in the path
                // BeatmapPath is typically like: C:\...\Songs\123456 Artist - Title\file.osu
                var key = ExtractMapKeyFromPath(beatmapPath);
                
                if (playCounts.ContainsKey(key))
                    playCounts[key] += count;
                else
                    playCounts[key] = count;
            }

            return playCounts;
        }

        /// <summary>
        /// Gets the folder name from a beatmap path.
        /// </summary>
        private string? GetFolderName(string beatmapPath)
        {
            try
            {
                var folder = Path.GetDirectoryName(beatmapPath);
                if (string.IsNullOrEmpty(folder))
                    return null;
                return Path.GetFileName(folder);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts "Artist - Title" from a beatmap path.
        /// osu! paths are typically: ...\Songs\123456 Artist - Title\difficulty.osu
        /// </summary>
        private string ExtractMapKeyFromPath(string beatmapPath)
        {
            try
            {
                // Get the folder containing the .osu file
                var folder = Path.GetDirectoryName(beatmapPath);
                if (string.IsNullOrEmpty(folder))
                    return beatmapPath;
                    
                var folderName = Path.GetFileName(folder);
                if (string.IsNullOrEmpty(folderName))
                    return beatmapPath;
                
                // Remove leading beatmap set ID (numbers followed by space)
                var trimmed = folderName.TrimStart();
                var spaceIndex = 0;
                while (spaceIndex < trimmed.Length && char.IsDigit(trimmed[spaceIndex]))
                {
                    spaceIndex++;
                }
                
                if (spaceIndex > 0 && spaceIndex < trimmed.Length && trimmed[spaceIndex] == ' ')
                {
                    return trimmed.Substring(spaceIndex + 1);
                }
                
                return folderName;
            }
            catch
            {
                return beatmapPath;
            }
        }
    }
}
