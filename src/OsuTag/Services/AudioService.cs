using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace OsuTag.Services
{
    /// <summary>
    /// Singleton service for cross-platform audio playback using native platform APIs.
    /// This implementation avoids heavy dependencies like LibVLC to keep the application size minimal.
    /// </summary>
    public class AudioService : IDisposable
    {
        private static readonly Lazy<AudioService> _instance = new(() => new AudioService());
        public static AudioService Instance => _instance.Value;

        private bool _isInitialized = false;
        private bool _useWindowsNative = false;
        private bool _useMacNative = false;
        private int _volume = (int)SettingsService.Settings.PreviewVolume;
        private CancellationTokenSource? _playbackCancellation;
        private string? _currentPlayingPath;
        private readonly object _playbackLock = new object();

        public int Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                // Update volume in settings
                SettingsService.Settings.PreviewVolume = value;
            }
        }

#if WINDOWS
        // Windows Native Player using Windows Media Player COM
        internal static class WindowsNativePlayer
        {
            private static dynamic? _player;
            private static readonly object _playerLock = new object();

            public static void Play(string path, int startTimeMs, float volume)
            {
                Task.Run(() => {
                    try
                    {
                        // Validate file exists
                        if (!File.Exists(path))
                        {
                            Console.WriteLine($"[AudioService] Audio file not found: {path}");
                            return;
                        }

                        lock (_playerLock)
                        {
                            try
                            {
                                // Create Windows Media Player COM object
                                if (_player == null)
                                {
                                    Type? playerType = Type.GetTypeFromProgID("WMPlayer.OCX");
                                    if (playerType != null)
                                    {
                                        _player = Activator.CreateInstance(playerType);
                                        Console.WriteLine("[AudioService] Created Windows Media Player COM object");
                                    }
                                    else
                                    {
                                        Console.WriteLine("[AudioService] ERROR: Could not create Windows Media Player COM object");
                                        return;
                                    }
                                }

                                // Stop any current playback
                                try { _player.controls.stop(); } catch { }

                                // Set the URL
                                _player.URL = path;
                                
                                // Set volume (0-100 range)
                                _player.settings.volume = (int)Math.Clamp(volume, 0, 100);
                                
                                // Wait a bit for the file to load
                                System.Threading.Thread.Sleep(100);
                                
                                // Seek to preview time (in seconds)
                                if (startTimeMs > 0)
                                {
                                    double startTimeSec = startTimeMs / 1000.0;
                                    _player.controls.currentPosition = startTimeSec;
                                }
                                
                                // Play
                                _player.controls.play();
                                
                                Console.WriteLine($"[AudioService] Playing preview: {Path.GetFileName(path)} at {startTimeMs}ms, volume: {volume}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[AudioService] Exception in Windows Media Player: {ex.Message}");
                                // Try to recreate player on next attempt
                                _player = null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AudioService] Exception in Play: {ex.Message}");
                    }
                });
            }

            public static void Stop()
            {
                Task.Run(() => {
                    try
                    {
                        lock (_playerLock)
                        {
                            if (_player != null)
                            {
                                try
                                {
                                    _player.controls.stop();
                                    _player.close();
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                });
            }
        }
#endif

#if !WINDOWS
        // macOS Native Player using P/Invoke to NSSound
        internal static class MacNativePlayer
        {
            [DllImport("/usr/lib/libobjc.A.dylib")]
            private static extern IntPtr objc_getClass(string name);

            [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

            [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

            [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static extern IntPtr objc_msgSend_IntPtr_IntPtr_byte(IntPtr receiver, IntPtr selector, IntPtr arg1, byte arg2);

            [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static extern void objc_msgSend_void_double(IntPtr receiver, IntPtr selector, double arg);

            [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static extern void objc_msgSend_void_float(IntPtr receiver, IntPtr selector, float arg);

            [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static extern double objc_msgSend_double(IntPtr receiver, IntPtr selector);

            [DllImport("/usr/lib/libobjc.A.dylib")]
            private static extern IntPtr sel_registerName(string name);

            private static IntPtr _nsSoundClass = objc_getClass("NSSound");
            private static IntPtr _allocSel = sel_registerName("alloc");
            private static IntPtr _initWithFileSel = sel_registerName("initWithContentsOfFile:byReference:");
            private static IntPtr _playSel = sel_registerName("play");
            private static IntPtr _stopSel = sel_registerName("stop");
            private static IntPtr _setCurrentTimeSel = sel_registerName("setCurrentTime:");
            private static IntPtr _setVolumeSel = sel_registerName("setVolume:");
            private static IntPtr _releaseSel = sel_registerName("release");
            private static IntPtr _durationSel = sel_registerName("duration");

            private static IntPtr _currentSound = IntPtr.Zero;

            public static void Play(string path, int startTimeMs, float volume)
            {
                Stop();

                try
                {
                    IntPtr nsPath = CreateNSString(path);
                    IntPtr soundAlloc = objc_msgSend_IntPtr(_nsSoundClass, _allocSel);
                    _currentSound = objc_msgSend_IntPtr_IntPtr_byte(soundAlloc, _initWithFileSel, nsPath, (byte)0);
                    
                    if (_currentSound != IntPtr.Zero)
                    {
                        double duration = objc_msgSend_double(_currentSound, _durationSel);
                        float volToSet = (float)(volume / 100.0);
                        objc_msgSend_void_float(_currentSound, _setVolumeSel, volToSet);
                        
                        double targetTime = startTimeMs / 1000.0;
                        if (targetTime < duration)
                        {
                            objc_msgSend_void_double(_currentSound, _setCurrentTimeSel, targetTime);
                        }
                        
                        objc_msgSend_IntPtr(_currentSound, _playSel);
                    }
                }
                catch { }
            }

            public static void Stop()
            {
                if (_currentSound != IntPtr.Zero)
                {
                    objc_msgSend_IntPtr(_currentSound, _stopSel);
                    objc_msgSend_IntPtr(_currentSound, _releaseSel);
                    _currentSound = IntPtr.Zero;
                }
            }

            private static IntPtr CreateNSString(string str)
            {
                IntPtr nsStringClass = objc_getClass("NSString");
                IntPtr sel = sel_registerName("stringWithUTF8String:");
                IntPtr utf8String = Marshal.StringToHGlobalAnsi(str);
                try
                {
                    return objc_msgSend_IntPtr_IntPtr(nsStringClass, sel, utf8String);
                }
                finally
                {
                    Marshal.FreeHGlobal(utf8String);
                }
            }
        }
#endif

        private AudioService()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            Console.WriteLine("[AudioService] Initializing audio service...");

            if (PlatformService.IsWindows)
            {
                _useWindowsNative = true;
                Console.WriteLine("[AudioService] Using Windows native audio (mciSendString)");
            }
            else if (PlatformService.IsMacOS)
            {
                _useMacNative = true;
                Console.WriteLine("[AudioService] Using macOS native audio (NSSound)");
            }
            else
            {
                Console.WriteLine("[AudioService] WARNING: No native audio support for this platform");
            }
            
            Console.WriteLine($"[AudioService] Initial volume: {_volume}");
            _isInitialized = true;
        }

        public void PlayPreview(string path, int startTimeMs, int? volume = null)
        {
            if (!_isInitialized) Initialize();

            Console.WriteLine($"[AudioService] PlayPreview called: {Path.GetFileName(path)}");

            // Debouncing: Cancel any pending playback
            lock (_playbackLock)
            {
                _playbackCancellation?.Cancel();
                _playbackCancellation = new CancellationTokenSource();
            }

            var currentToken = _playbackCancellation;

            try
            {
                // Check if this playback was cancelled
                if (currentToken.Token.IsCancellationRequested)
                {
                    Console.WriteLine("[AudioService] Playback was cancelled before starting");
                    return;
                }

                // Prevent playing the same file if already playing
                lock (_playbackLock)
                {
                    if (_currentPlayingPath == path)
                    {
                        Console.WriteLine("[AudioService] Already playing this file, skipping");
                        return;
                    }
                    _currentPlayingPath = path;
                }

                int finalVolume = volume ?? _volume;
                Console.WriteLine($"[AudioService] Volume: {finalVolume}, UseWindowsNative: {_useWindowsNative}");

                if (_useWindowsNative)
                {
#if WINDOWS
                    WindowsNativePlayer.Play(path, startTimeMs, finalVolume);
#endif
                    return;
                }

                if (_useMacNative)
                {
#if !WINDOWS
                    MacNativePlayer.Play(path, startTimeMs, finalVolume);
#endif
                    return;
                }

                Console.WriteLine("[AudioService] WARNING: No audio backend available!");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("[AudioService] Playback was cancelled (TaskCanceledException)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioService] Error in PlayPreview: {ex.Message}");
                Console.WriteLine($"[AudioService] Stack trace: {ex.StackTrace}");
            }
        }

        public void Stop()
        {
            Console.WriteLine("[AudioService] Stop called");

            // Cancel any pending playback
            lock (_playbackLock)
            {
                _playbackCancellation?.Cancel();
                _currentPlayingPath = null;
            }

            if (_useWindowsNative)
            {
#if WINDOWS
                WindowsNativePlayer.Stop();
#endif
            }
            else if (_useMacNative)
            {
#if !WINDOWS
                MacNativePlayer.Stop();
#endif
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
