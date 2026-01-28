using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MiniaudioSharp;

namespace Osutag.Services
{
    public unsafe class AudioService : IDisposable
    {
        private static AudioService? _instance;
        public static AudioService Instance => _instance ??= new AudioService();

        private ma_engine* _engine;
        private ma_sound* _sound;
        private bool _isInitialized = false;
        private string? _currentPath;
        private readonly object _lock = new();
        private CancellationTokenSource? _playCts;

        private int _volume = (int)SettingsService.Settings.PreviewVolume;
        public int Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                SettingsService.Settings.PreviewVolume = value;
                lock (_lock)
                {
                    if (_isInitialized && _sound != null)
                    {
                        Miniaudio.ma_sound_set_volume(_sound, _volume / 100f);
                    }
                }
            }
        }

        public event EventHandler<bool>? IsLoadingChanged;
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    IsLoadingChanged?.Invoke(this, value);
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
                // ma_engine is quite large, and if the binding is opaque, sizeof() might be 1 or 4.
                // We allocate a safe margin of 64KB to avoid memory corruption during ma_engine_init.
                _engine = (ma_engine*)Marshal.AllocHGlobal(64 * 1024);
                
                // Clear memory
                byte* ptr = (byte*)_engine;
                for(int i=0; i<64*1024; i++) ptr[i] = 0;

                var result = Miniaudio.ma_engine_init(null, _engine);
                if (result == ma_result.MA_SUCCESS)
                {
                    _isInitialized = true;
                }
                else
                {
                    Marshal.FreeHGlobal((IntPtr)_engine);
                    _engine = null;
                }
            }
            catch { }
        }

        public void PlayPreview(string path, int startTimeMs, int? durationMs = null, float rate = 1.0f, bool maintainPitch = false)
        {
            if (!_isInitialized) return;

            lock (_lock)
            {
                if (_currentPath == path && _sound != null) return;
                
                // Cancel previous loading task
                _playCts?.Cancel();
                _playCts = new CancellationTokenSource();
            }

            var token = _playCts.Token;
            
            // Move entire operation off the UI thread
            Task.Run(() =>
            {
                try
                {
                    // 1. STOP PREVIOUS (Inside background thread, but with lock)
                    StopInternal();

                    if (token.IsCancellationRequested) return;

                    IsLoading = true;

                    // 2. LOAD NEW
                    var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(path + "\0");
                    fixed (byte* pPath = utf8Bytes)
                    {
                        var pSound = (ma_sound*)Marshal.AllocHGlobal(32 * 1024); // Safe margin for ma_sound
                        // Clear memory
                        byte* sPtr = (byte*)pSound;
                        for(int i=0; i<32*1024; i++) sPtr[i] = 0;

                        var result = Miniaudio.ma_sound_init_from_file(_engine, (sbyte*)pPath, 0, null, null, pSound);
                        
                        if (result != ma_result.MA_SUCCESS)
                        {
                            Marshal.FreeHGlobal((IntPtr)pSound);
                            return;
                        }

                        if (token.IsCancellationRequested)
                        {
                            Miniaudio.ma_sound_uninit(pSound);
                            Marshal.FreeHGlobal((IntPtr)pSound);
                            return;
                        }

                        lock (_lock)
                        {
                            _sound = pSound;
                            _currentPath = path;
                        }
                    }

                    // 3. CONFIGURE & START
                    lock (_lock)
                    {
                        if (_sound == null) return;
                        Miniaudio.ma_sound_set_volume(_sound, _volume / 100f);
                        Miniaudio.ma_sound_set_pitch(_sound, rate);

                        if (startTimeMs > 0)
                        {
                            var pDataSource = Miniaudio.ma_sound_get_data_source(_sound);
                            if (pDataSource != null)
                            {
                                ma_format format;
                                uint channels;
                                uint sampleRate;
                                Miniaudio.ma_data_source_get_data_format(pDataSource, &format, &channels, &sampleRate, null, 0);
                                if (sampleRate > 0)
                                {
                                    ulong frame = (ulong)((double)startTimeMs / 1000.0 * sampleRate);
                                    Miniaudio.ma_sound_seek_to_pcm_frame(_sound, frame);
                                }
                            }
                        }
                        Miniaudio.ma_sound_start(_sound);
                    }
                }
                catch { }
                finally
                {
                    IsLoading = false;
                }
            }, token);
        }

        public void UpdatePlaybackState(float rate, bool maintainPitch)
        {
            lock (_lock)
            {
                if (_isInitialized && _sound != null)
                {
                    if (!maintainPitch)
                    {
                        // Coupled Mode: Changes both speed and pitch
                        Miniaudio.ma_sound_set_pitch(_sound, rate);
                    }
                    else
                    {
                        // Independent Mode: Attempt speed change without pitch shift
                        // Since ma_sound_set_speed isn't available, we'll try a fallback
                        // Or we just reset to 1.0 for consistency if we can't do it.
                        // However, to make it 'live', we'll stick to resampling for now
                        // but maybe we can find a way to set speed.
                        Miniaudio.ma_sound_set_pitch(_sound, rate);
                    }
                }
            }
        }

        public void Stop()
        {
            _playCts?.Cancel();
            Task.Run(() => StopInternal());
        }

        private void StopInternal()
        {
            lock (_lock)
            {
                _currentPath = null;
                if (_sound != null)
                {
                    Miniaudio.ma_sound_stop(_sound);
                    Miniaudio.ma_sound_uninit(_sound);
                    Marshal.FreeHGlobal((IntPtr)_sound);
                    _sound = null;
                }
            }
        }

        public void Dispose()
        {
            _playCts?.Cancel();
            StopInternal();
            lock (_lock)
            {
                if (_isInitialized && _engine != null)
                {
                    Miniaudio.ma_engine_uninit(_engine);
                    Marshal.FreeHGlobal((IntPtr)_engine);
                    _engine = null;
                    _isInitialized = false;
                }
            }
        }
    }
}
