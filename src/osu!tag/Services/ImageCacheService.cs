using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Osutag.Services
{
    /// <summary>
    /// High-performance async image cache service.
    /// All decoding happens on background threads to prevent UI stutter.
    /// </summary>
    public sealed class ImageCacheService
    {
        private static readonly Lazy<ImageCacheService> _instance = new(() => new ImageCacheService());
        public static ImageCacheService Instance => _instance.Value;

        // LRU cache with max 500 entries (each ~200px wide bitmap ~50KB = ~25MB max)
        private const int MaxCacheSize = 500;
        private const int DecodeWidth = 200; // Sufficient for 180px card display

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly ConcurrentDictionary<string, Task<Bitmap?>> _pendingLoads = new();
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);
        private int _accessCounter = 0;

        private sealed class CacheEntry
        {
            public Bitmap? Bitmap { get; set; }
            public int LastAccess { get; set; }
        }

        private ImageCacheService() { }

        /// <summary>
        /// Gets a cached bitmap or loads it asynchronously from disk.
        /// Returns null if file doesn't exist or fails to decode.
        /// Thread-safe and deduplicated (multiple calls for same path share one load).
        /// </summary>
        public async Task<Bitmap?> GetImageAsync(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            // Check cache first
            if (_cache.TryGetValue(path, out var entry))
            {
                entry.LastAccess = Interlocked.Increment(ref _accessCounter);
                return entry.Bitmap;
            }

            // Deduplicate concurrent loads for same path
            var loadTask = _pendingLoads.GetOrAdd(path, p => LoadImageAsync(p));

            try
            {
                var bitmap = await loadTask.ConfigureAwait(false);

                // Cache the result
                if (bitmap != null)
                {
                    var newEntry = new CacheEntry
                    {
                        Bitmap = bitmap,
                        LastAccess = Interlocked.Increment(ref _accessCounter)
                    };

                    _cache[path] = newEntry;

                    // Trigger cleanup if cache is too large
                    if (_cache.Count > MaxCacheSize)
                    {
                        _ = CleanupCacheAsync();
                    }
                }

                return bitmap;
            }
            finally
            {
                _pendingLoads.TryRemove(path, out _);
            }
        }

        /// <summary>
        /// Synchronous cache lookup only. Returns null if not cached.
        /// Use this for immediate display without waiting.
        /// </summary>
        public Bitmap? GetCachedImage(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (_cache.TryGetValue(path, out var entry))
            {
                entry.LastAccess = Interlocked.Increment(ref _accessCounter);
                return entry.Bitmap;
            }

            return null;
        }

        /// <summary>
        /// Preloads multiple images in parallel for upcoming display.
        /// </summary>
        public async Task PreloadAsync(IEnumerable<string?> paths, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();

            foreach (var path in paths)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (!string.IsNullOrEmpty(path) && !_cache.ContainsKey(path))
                {
                    tasks.Add(GetImageAsync(path));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        private static Task<Bitmap?> LoadImageAsync(string path)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(path))
                        return null;

                    using var stream = File.OpenRead(path);
                    // DecodeToWidth is more efficient than full decode for thumbnails
                    return Bitmap.DecodeToWidth(stream, DecodeWidth);
                }
                catch
                {
                    // File not found, invalid format, etc.
                    return null;
                }
            });
        }

        private async Task CleanupCacheAsync()
        {
            if (!await _cleanupLock.WaitAsync(0))
                return; // Another cleanup is already running

            try
            {
                if (_cache.Count <= MaxCacheSize)
                    return;

                // Remove oldest 20% of entries
                var toRemove = _cache.Count - (int)(MaxCacheSize * 0.8);
                
                var oldestEntries = _cache
                    .OrderBy(kvp => kvp.Value.LastAccess)
                    .Take(toRemove)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldestEntries)
                {
                    if (_cache.TryRemove(key, out var removed))
                    {
                        removed.Bitmap?.Dispose();
                    }
                }
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        /// <summary>
        /// Clears all cached images and disposes bitmaps.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _cache)
            {
                kvp.Value.Bitmap?.Dispose();
            }
            _cache.Clear();
            _pendingLoads.Clear();
        }
    }
}
