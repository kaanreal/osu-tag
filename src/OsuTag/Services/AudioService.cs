using System;
using System.IO;
using System.Runtime.InteropServices;
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
        private bool _useMacNativeFallback = false; // New field

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

#if !WINDOWS
    // macOS Native Player using P/Invoke to NSSound
    internal static class MacNativePlayer
    {
        private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
        
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

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern float objc_msgSend_float(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern byte objc_msgSend_byte(IntPtr receiver, IntPtr selector);

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
        private static IntPtr _isPlayingSel = sel_registerName("isPlaying");
        private static IntPtr _currentTimeSel = sel_registerName("currentTime");
        private static IntPtr _volumeSel = sel_registerName("volume");

        private static IntPtr _currentSound = IntPtr.Zero;

        public static void Play(string path, int startTimeMs, float volume)
        {
            Stop();

            try
            {
                IntPtr nsPath = CreateNSString(path);
                IntPtr soundAlloc = objc_msgSend_IntPtr(_nsSoundClass, _allocSel);
                _currentSound = objc_msgSend_IntPtr_IntPtr_byte(soundAlloc, _initWithFileSel, nsPath, (byte)0); // byReference: NO (load into memory)
                
                if (_currentSound != IntPtr.Zero)
                {
                    double duration = objc_msgSend_double(_currentSound, _durationSel);
                    
                    // Set volume (0.0 to 1.0)
                    float volToSet = (float)(volume / 100.0);
                    objc_msgSend_void_float(_currentSound, _setVolumeSel, volToSet);
                    
                    // Set start time
                    double targetTime = startTimeMs / 1000.0;
                    if (targetTime < duration)
                    {
                        objc_msgSend_void_double(_currentSound, _setCurrentTimeSel, targetTime);
                    }
                    else
                    {
                        Console.WriteLine($"[AudioService] Warning: Start time {targetTime}s exceeds duration {duration}s");
                    }
                    
                    // Play
                    objc_msgSend_IntPtr(_currentSound, _playSel);
                    
                    bool isPlaying = objc_msgSend_byte(_currentSound, _isPlayingSel) != 0;
                    double actualTime = objc_msgSend_double(_currentSound, _currentTimeSel);
                    float actualVol = objc_msgSend_float(_currentSound, _volumeSel);

                    Console.WriteLine($"[AudioService] NSSound Info -> File: {Path.GetFileName(path)}");
                    Console.WriteLine($"[AudioService]   - Duration: {duration:F2}s");
                    Console.WriteLine($"[AudioService]   - Requested Time: {targetTime}s, Actual: {actualTime:F2}s");
                    Console.WriteLine($"[AudioService]   - Requested Vol: {volToSet:F2}, Actual: {actualVol:F2}");
                    Console.WriteLine($"[AudioService]   - IsPlaying: {isPlaying}");
                }
                else
                {
                    Console.WriteLine($"[AudioService] NSSound failed to load file: {path}");
                }
                // DO NOT Marshal.FreeHGlobal(nsPath) here. nsPath is an Objective-C object, not a C-string.
                // The C-string was already freed inside CreateNSString.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioService] NSSound Error: {ex.Message}");
            }
        }

        public static void Stop()
        {
            if (_currentSound != IntPtr.Zero)
            {
                objc_msgSend_IntPtr(_currentSound, _stopSel);
                objc_msgSend_IntPtr(_currentSound, _releaseSel); // Release the object
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

        public void Initialize() // Changed from private to public as per diff
        {
            if (_isInitialized) return;

            try
            {
                if (PlatformService.IsWindows)
                {
                    string libvlcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc", IntPtr.Size == 8 ? "win-x64" : "win-x86");
                    System.Diagnostics.Debug.WriteLine($"[AudioService] Windows Init: {libvlcPath}");
                    Core.Initialize(libvlcPath);
                }
                else if (PlatformService.IsMacOS)
                {
                    // Prioritize native fallback on ARM64 to avoid native crashes from mismatched LibVLC dylibs
                    bool isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
                    if (isArm64)
                    {
                        Console.WriteLine("[AudioService] detected macOS ARM64. Using NSSound native fallback directly.");
                        _useMacNativeFallback = true;
                        _isInitialized = true;
                        return;
                    }

                    Console.WriteLine("[AudioService] Detected macOS x64. Attempting LibVLC init...");
                    try
                    {
                        Core.Initialize();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AudioService] LibVLC Core.Initialize failed: {ex.Message}. Falling back to NSSound.");
                        _useMacNativeFallback = true;
                        _isInitialized = true;
                        return; // Exit initialization, use native player
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
                    System.Diagnostics.Debug.WriteLine($"[AudioService] LibVLC creation failed: {ex.Message}");
                    if (ex.InnerException != null)
                        System.Diagnostics.Debug.WriteLine($"[AudioService] Inner Exception: {ex.InnerException.Message}");

                    if (PlatformService.IsMacOS)
                    {
                        System.Diagnostics.Debug.WriteLine("[AudioService] Switching to NSSound fallback.");
                        _useMacNativeFallback = true;
                    }
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
                if (PlatformService.IsMacOS) _useMacNativeFallback = true;
                _isInitialized = true;
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

            if (_useMacNativeFallback)
            {
#if !WINDOWS
                MacNativePlayer.Play(path, startTimeMs, volume ?? (int)SettingsService.Settings.PreviewVolume); // Use SettingsService volume if not provided
#endif
                return;
            }

            if (_libVLC == null || _mediaPlayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[AudioService] PlayPreview aborted: Service not initialized corectly.");
                return;
            }

            try
            {
                Stop();

                if (!File.Exists(path))
                {
                    Console.WriteLine($"[AudioService] File not found: {path}");
                    return;
                }

                // Create media from path
                _currentMedia = new Media(_libVLC, path, FromType.FromPath);
                
                // Add options
                _currentMedia.AddOption(":no-video");
                _currentMedia.AddOption(":no-spu");
                
                // Set start time for preview if supported
                if (startTimeMs > 0)
                {
                    _currentMedia.AddOption($":start-time={startTimeMs / 1000.0}");
                }

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Media = _currentMedia;
                    _mediaPlayer.Volume = volume ?? (int)SettingsService.Settings.PreviewVolume; // Use SettingsService volume if not provided
                    
                    // Play
                    _mediaPlayer.Play();

                    // On some systems, volume needs to be set AFTER Play starts
                    // This line is removed as per instruction, and volume is set before Play()
                    // _mediaPlayer.Volume = finalVolume; 

                    System.Diagnostics.Debug.WriteLine($"Playback started: {_mediaPlayer.IsPlaying}, Volume: {_mediaPlayer.Volume}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioService] Playback Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the current playback.
        /// </summary>
        public void Stop()
        {
            if (_useMacNativeFallback)
            {
#if !WINDOWS
                MacNativePlayer.Stop();
#endif
                return;
            }

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
