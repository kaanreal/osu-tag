using System;
using System.IO;
using LibVLCSharp.Shared;

namespace OsuTag.Services
{
    /// <summary>
    /// Singleton service for cross-platform audio playback using LibVLCSharp.
    /// handles song previews with seeking support.
    /// </summary>
    public class AudioService : IDisposable
    {
        private static readonly Lazy<AudioService> _instance = new(() => new AudioService());
        public static AudioService Instance => _instance.Value;

        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private bool _isInitialized = false;

        public int Volume
        {
            get => _mediaPlayer?.Volume ?? (int)SettingsService.Settings.PreviewVolume;
            set
            {
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

        private void Initialize()
        {
            try
            {
                if (_isInitialized) return;

                if (PlatformService.IsWindows)
                {
                    string libvlcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc", IntPtr.Size == 8 ? "win-x64" : "win-x86");
                    System.Diagnostics.Debug.WriteLine($"[AudioService] Windows Init: {libvlcPath}");
                    Core.Initialize(libvlcPath);
                }
                else if (PlatformService.IsMacOS)
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    System.Diagnostics.Debug.WriteLine($"[AudioService] macOS Init. Base: {baseDir}");
                    
                    try 
                    {
                        // Multiple common paths on macOS
                        string[] possiblePaths = new[]
                        {
                            baseDir,
                            Path.Combine(baseDir, "runtimes", "osx", "native"),
                            Path.Combine(baseDir, "runtimes", "osx-x64", "native"),
                            Path.Combine(baseDir, "runtimes", "osx-arm64", "native")
                        };

                        bool success = false;
                        foreach (var path in possiblePaths)
                        {
                            if (Directory.Exists(path))
                            {
                                try
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AudioService] Testing path: {path}");
                                    Core.Initialize(path);
                                    success = true;
                                    System.Diagnostics.Debug.WriteLine($"[AudioService] Core.Initialize succeeded for: {path}");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AudioService] Core.Initialize failed for {path}: {ex.Message}");
                                }
                            }
                        }

                        if (!success)
                        {
                            System.Diagnostics.Debug.WriteLine("[AudioService] All specific paths failed, calling default Core.Initialize()");
                            Core.Initialize();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AudioService] Critical error in Core.Initialize: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[AudioService] Linux Init");
                    Core.Initialize();
                }

                try 
                {
                    _libVLC = new LibVLC("--verbose=2", "--no-video", "--no-spu", "--no-lua"); 
                    _mediaPlayer = new MediaPlayer(_libVLC);
                    System.Diagnostics.Debug.WriteLine("[AudioService] LibVLC and MediaPlayer created successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AudioService] Failed to create LibVLC instance: {ex.Message}");
                    if (ex.InnerException != null)
                        System.Diagnostics.Debug.WriteLine($"[AudioService] Inner Exception: {ex.InnerException.Message}");
                }
                
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.EncounteredError += (s, e) => System.Diagnostics.Debug.WriteLine("[AudioService] LibVLC Error event");
                    _mediaPlayer.EndReached += (s, e) => System.Diagnostics.Debug.WriteLine("[AudioService] LibVLC EndReached");
                }
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService] Fatal Initialization Error: {ex.Message}");
            }
        }

        private Media? _currentMedia;

        /// <summary>
        /// Plays an audio file starting from a specific time.
        /// Stops any currently playing audio.
        /// </summary>
        /// <param name="path">Path to the MP3 file.</param>
        /// <param name="startTimeMs">Start time in milliseconds.</param>
        /// <param name="volume">Optional volume override (0-100).</param>
        public void PlayPreview(string path, int startTimeMs, int? volume = null)
        {
            if (!_isInitialized) 
            {
                Initialize();
            }

            if (_libVLC == null || _mediaPlayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[AudioService] PlayPreview aborted: Service not initialized corectly.");
                return;
            }

            try
            {
                if (!File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"Audio file not found: {path}");
                    return;
                }

                int finalVolume = volume ?? (int)SettingsService.Settings.PreviewVolume;
                System.Diagnostics.Debug.WriteLine($"Playing preview: {path} at {startTimeMs}ms with volume {finalVolume}");

                Stop();

                if (_mediaPlayer == null || _libVLC == null)
                {
                    System.Diagnostics.Debug.WriteLine("AudioService: MediaPlayer or LibVLC is null even after initialization attempt.");
                    return;
                }

                // Create media from path
                _currentMedia = new Media(_libVLC, path, FromType.FromPath);
                
                // Add options
                _currentMedia.AddOption(":no-video");
                _currentMedia.AddOption(":no-spu");
                
                if (startTimeMs > 0)
                {
                    _currentMedia.AddOption($":start-time={startTimeMs / 1000.0}");
                }

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Media = _currentMedia;
                    _mediaPlayer.Volume = finalVolume;
                    
                    // Play
                    _mediaPlayer.Play();

                    // On some systems, volume needs to be set AFTER Play starts
                    _mediaPlayer.Volume = finalVolume;

                    System.Diagnostics.Debug.WriteLine($"Playback started: {_mediaPlayer.IsPlaying}, Volume: {_mediaPlayer.Volume}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing preview: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the current playback.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_mediaPlayer?.IsPlaying == true)
                {
                    _mediaPlayer.Stop();
                }
                
                _currentMedia?.Dispose();
                _currentMedia = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping playback: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _currentMedia?.Dispose();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
        }
    }
}
