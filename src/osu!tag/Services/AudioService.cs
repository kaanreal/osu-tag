using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace Osutag.Services
{
    /// <summary>
    /// Singleton service for cross-platform audio playback using LibVLCSharp.
    /// Uses lazy initialization - VLC only loads on first audio play.
    /// </summary>
    public class AudioService : IDisposable
    {
        private static readonly Lazy<AudioService> _instance = new(() => new AudioService());
        public static AudioService Instance => _instance.Value;

        private bool _isInitialized = false;
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        
        // Loading state for UI
        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    IsLoadingChanged?.Invoke(this, value);
                }
            }
        }
        public event EventHandler<bool>? IsLoadingChanged;
        
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
                
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = value;
                }
            }
        }

        private AudioService()
        {
            // Don't initialize here - lazy init on first play
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            lock (_vlcLock)
            {
                if (_isInitialized) return;

                try
                {
                    IsLoading = true;
                    
                    Core.Initialize();

                    // Suppress VLC warnings/logs with --quiet
                    _libVLC = new LibVLC("--quiet", "--no-video");
                    _mediaPlayer = new MediaPlayer(_libVLC);
                    
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Audio] Failed to initialize: {ex.Message}");
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        public void PlayPreview(string path, int startTimeMs, int? volume = null)
        {
            Task.Run(() =>
            {
                CancellationToken token;
                
                lock (_playbackLock)
                {
                    _playbackCancellation?.Cancel();
                    _playbackCancellation = new CancellationTokenSource();
                    token = _playbackCancellation.Token;
                    _currentPlayingPath = path;
                }

                if (token.IsCancellationRequested) return;

                int finalVolume = volume ?? (int)SettingsService.Settings.PreviewVolume;
                if (finalVolume <= 0) return;

                // Lazy init - only loads VLC on first play
                Initialize();
                if (_libVLC == null || _mediaPlayer == null) return;

                lock (_vlcLock)
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;

                        string mediaPath = path;
                        if (!path.Contains("://") && File.Exists(path))
                        {
                            mediaPath = new Uri(path).AbsoluteUri;
                        }

                        using var media = new Media(_libVLC, mediaPath, FromType.FromLocation);
                        media.AddOption(":no-video");
                        
                        _mediaPlayer.Media = media;
                        _mediaPlayer.Volume = finalVolume;
                        
                        if (!_mediaPlayer.Play()) return;

                        if (startTimeMs > 0)
                        {
                            _mediaPlayer.Time = startTimeMs;
                        }
                    }
                    catch { }
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
                if (_mediaPlayer?.IsPlaying == true)
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
