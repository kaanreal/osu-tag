using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Osutag.Services
{
    /// <summary>
    /// Singleton service for audio playback using NAudio.
    /// Supports Varispeed (Chipmunk mode).
    /// </summary>
    public class AudioService : IDisposable
    {
        private static readonly Lazy<AudioService> _instance = new(() => new AudioService());
        public static AudioService Instance => _instance.Value;

        private IWavePlayer? _waveOut;
        private AudioFileReader? _audioFileReader;
        private VarispeedSampleProvider? _varispeed;
        private SoundTouchSampleProvider? _soundTouch;
        private VolumeSampleProvider? _volumeProvider;

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
        
        private CancellationTokenSource? _playbackCancellation;
        private readonly object _playbackLock = new object();
        private string? _currentPlayingPath;

        private int _volume = (int)SettingsService.Settings.PreviewVolume;
        public int Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                SettingsService.Settings.PreviewVolume = value;
                if (_volumeProvider != null)
                {
                    _volumeProvider.Volume = value / 100f;
                }
            }
        }

        private AudioService() { }

        public void PlayPreview(string path, int startTimeMs, int? volume = null, float rate = 1.0f, bool maintainPitch = true, float pitchSemitones = 0.0f)
        {
            Task.Run(() =>
            {
                lock (_playbackLock)
                {
                    StopSync(); // Stop existing playback

                    try
                    {
                        IsLoading = true;

                        string mediaPath = path;
                        if (!path.Contains("://") && File.Exists(path))
                        {
                            mediaPath = path;
                        }
                        else
                        {
                            return; // NAudio file reader needs local file usually, output URL streaming requires MediaFoundationReader
                        }
                        
                        _currentPlayingPath = mediaPath;

                        // Initialize NAudio
                        _waveOut = new WaveOutEvent();
                        _audioFileReader = new AudioFileReader(mediaPath);

                        // Varispeed Chain
                        ISampleProvider source = _audioFileReader;
                        
                        // Pipeline selection:
                        // If MaintainPitch = FALSE (Chipmunk): use Varispeed
                        // If MaintainPitch = TRUE (Tempo Shift): use SoundTouch
                        
                        ISampleProvider finalProvider;

                        if (maintainPitch)
                        {
                            // Use SoundTouch for Tempo Shift
                            _soundTouch = new SoundTouchSampleProvider(source);
                            _soundTouch.Tempo = rate;
                            // Reset Varispeed
                            _varispeed = null;
                            finalProvider = _soundTouch;
                        }
                        else
                        {
                            // Use Varispeed for Chipmunk
                            _varispeed = new VarispeedSampleProvider(source);
                            _varispeed.PlaybackRate = rate;
                            // Reset SoundTouch
                            _soundTouch = null;
                            finalProvider = _varispeed;
                        }

                        // Volume at the end
                        _volumeProvider = new VolumeSampleProvider(finalProvider);
                        _volumeProvider.Volume = (volume ?? _volume) / 100f;

                        _waveOut.Init(_volumeProvider);

                        if (startTimeMs > 0)
                        {
                            _audioFileReader.CurrentTime = TimeSpan.FromMilliseconds(startTimeMs);
                        }

                        _waveOut.Play();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NAudio] Playback failed: {ex.Message}");
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                }
            });
        }

        public void UpdatePlaybackState(float rate, bool maintainPitch)
        {
             // If mode switched (MaintainPitch changed), we MUST restart the pipeline
             // because we swap providers entirely.
             // WE can't just hot-swap easily without tearing down the wave player usually.
             // OR we could keep both initialized and mix, but restarting is safer/simpler for now.
             
             bool currentlyUsingSoundTouch = _soundTouch != null;
             bool wantSoundTouch = maintainPitch;

             if (currentlyUsingSoundTouch != wantSoundTouch)
             {
                 // Mode change detected - auto-restart at current position
                 if (_audioFileReader != null && _currentPlayingPath != null)
                 {
                     string path = _currentPlayingPath;
                     int position = (int)_audioFileReader.CurrentTime.TotalMilliseconds;
                     
                     // Restart with new mode
                     // This runs in background thread via PlayPreview
                     PlayPreview(path, position, null, rate, maintainPitch);
                 }
             }
             else
             {
                 // Same mode, just update rate
                 if (_varispeed != null) _varispeed.PlaybackRate = rate;
                 if (_soundTouch != null) _soundTouch.Tempo = rate;
             }
        }

        public void Stop()
        {
            Task.Run(() =>
            {
                lock (_playbackLock)
                {
                    StopSync();
                }
            });
        }

        private void StopSync()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            
            _audioFileReader?.Dispose();
            _audioFileReader = null;
            
            _varispeed = null;
            _soundTouch = null;
            _volumeProvider = null;
        }

        public void Dispose()
        {
            StopSync();
        }
        
        // Stub for existing calls
        public void Initialize() { }
    }
}
