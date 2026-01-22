using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace Osutag.Services
{
    /// <summary>
    /// Singleton service for cross-platform audio playback using LibVLCSharp.
    /// This implementation provides consistent behavior across Windows, macOS, and Linux.
    /// </summary>
    public class AudioService : IDisposable
    {
        private static readonly Lazy<AudioService> _instance = new(() => new AudioService());
        public static AudioService Instance => _instance.Value;

        private bool _isInitialized = false;
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        
        // Debouncing
        private CancellationTokenSource? _playbackCancellation;
        private string? _currentPlayingPath;
        private readonly object _playbackLock = new object();
        private readonly object _vlcLock = new object();

        private int _volume = (int)SettingsService.Settings.PreviewVolume;
        public int Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                SettingsService.Settings.PreviewVolume = value;
                
                // Update live if playing
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = value;
                }
            }
        }

        private AudioService()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // Initialize Core. native libraries must be deployed.
                Core.Initialize();

                // Create LibVLC with default options
                _libVLC = new LibVLC();
                
                // Create MediaPlayer
                _mediaPlayer = new MediaPlayer(_libVLC);
                
                _isInitialized = true;
                Console.WriteLine("[Audio] LibVLC Initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Failed to initialize LibVLC: {ex.Message}");
            }
        }

        public void PlayPreview(string path, int startTimeMs, int? volume = null)
        {
            if (!_isInitialized) Initialize();
            if (_libVLC == null || _mediaPlayer == null) return;

            // Offload to thread to keep UI snappy, though LibVLC is async-friendly.
            Task.Run(() =>
            {
                CancellationToken token;
                
                lock (_playbackLock)
                {
                    _playbackCancellation?.Cancel();
                    _playbackCancellation = new CancellationTokenSource();
                    token = _playbackCancellation.Token;

                    if (_currentPlayingPath == path && _mediaPlayer.IsPlaying) 
                    {
                        // Already playing this track, maybe just seek?
                        // For now, simpler to just restart to ensure preview point is hit.
                    }
                    _currentPlayingPath = path;
                }

                if (token.IsCancellationRequested) return;

                int finalVolume = volume ?? (int)SettingsService.Settings.PreviewVolume;
                if (finalVolume <= 0) return;

                lock (_vlcLock)
                {
                    try
                    {
                        // Local path handling
                        string mediaPath = path;
                        if (!path.Contains("://") && File.Exists(path))
                        {
                            // LibVLC sometimes prefers file:// URI for local files to handle chars better
                            mediaPath = new Uri(path).AbsoluteUri;
                        }

                        // Create media from path
                        using var media = new Media(_libVLC, mediaPath, FromType.FromLocation);

                        // Optimizations for faster seek/start
                        media.AddOption(":no-video"); 
                        
                        _mediaPlayer.Media = media;
                        _mediaPlayer.Volume = finalVolume;
                        
                        // Play immediately
                        bool playResult = _mediaPlayer.Play();
                        
                        if (!playResult)
                        {
                            Console.WriteLine($"[Audio] LibVLC Play() returned false for {path}");
                            return;
                        }

                        // Handle Seeking
                        if (startTimeMs > 0)
                        {
                            // LibVLC expects time in milliseconds directly.
                            _mediaPlayer.Time = startTimeMs;
                            Console.WriteLine($"[Audio] Playing {Path.GetFileName(path)} from {startTimeMs}ms");
                        }
                        else
                        {
                             Console.WriteLine($"[Audio] Playing {Path.GetFileName(path)} from start");
                        }
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"[Audio] LibVLC Error: {ex.Message}");
                    }
                }
            });
        }

        public void Stop()
        {
            lock (_playbackLock)
            {
                _playbackCancellation?.Cancel();
                _currentPlayingPath = null;
            }

            lock (_vlcLock)
            {
                if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
                {
                    _mediaPlayer.Stop();
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
        }
    }
}
