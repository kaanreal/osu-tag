using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

#if WINDOWS
        // Windows Native Player using mciSendString (winmm.dll)
        internal static class WindowsNativePlayer
        {
            [DllImport("winmm.dll", CharSet = CharSet.Auto)]
            private static extern long mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr winHandle);

            public static void Play(string path, int startTimeMs, float volume)
            {
                Task.Run(() => {
                    StopInternal();
                    
                    // MCI is incredibly picky. We will try a multi-stage approach.
                    string[] variations = {
                        $"open \"{path}\" type mpegvideo alias preview", // Stage 1: Standard Quoted
                        $"open \"{path}\" alias preview",                // Stage 2: Quoted, No Type (Auto-detect)
                        $"open {path} alias preview",                    // Stage 3: Unquoted, No Type (Legacy)
                        $"open \"{GetShortPathName(path)}\" type mpegvideo alias preview", // Stage 4: Short Path (Unicode)
                        $"open \"{GetShortPathName(path)}\" alias preview" // Stage 5: Short Path, No Type
                    };

                    long res = -1;
                    foreach (var cmd in variations)
                    {
                        res = mciSendString(cmd, null, 0, IntPtr.Zero);
                        if (res == 0) break;
                    }

                    if (res == 0)
                    {
                        int vol = (int)Math.Clamp(volume * 10, 0, 1000);
                        mciSendString("setaudio preview volume to " + vol, null, 0, IntPtr.Zero);
                        mciSendString("play preview from " + startTimeMs, null, 0, IntPtr.Zero);
                    }
                    else
                    {
                        var errorMsg = new StringBuilder(255);
                        mciGetErrorString(res, errorMsg, errorMsg.Capacity);
                        Console.WriteLine($"[AudioService] Windows MCI failed all methods. Last error ({res}): {errorMsg}");
                        Console.WriteLine($"[AudioService] Attempted Path: {path}");
                    }
                });
            }

            public static void Stop() => Task.Run(() => StopInternal());

            private static void StopInternal()
            {
                mciSendString("stop preview", null, 0, IntPtr.Zero);
                mciSendString("close preview", null, 0, IntPtr.Zero);
            }

            private static string GetShortPathName(string path)
            {
                StringBuilder shortPath = new StringBuilder(255);
                GetShortPathName(path, shortPath, shortPath.Capacity);
                return shortPath.ToString();
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            private static extern int GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);

            [DllImport("winmm.dll", CharSet = CharSet.Auto)]
            private static extern long mciGetErrorString(long errorCode, StringBuilder errorText, int errorTextSize);
        }
#endif

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
                    
                    // Play
                    objc_msgSend_IntPtr(_currentSound, _playSel);
                }
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

        private bool _useWindowsNative = false;

        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                if (PlatformService.IsWindows)
                {
                    Console.WriteLine("[AudioService] Windows detected. Using winmm.dll native player.");
                    _useWindowsNative = true;
                    _isInitialized = true;
                    return;
                }
                else if (PlatformService.IsMacOS)
                {
                    Console.WriteLine("[AudioService] macOS detected. Using NSSound native fallback.");
                    _useMacNativeFallback = true;
                    _isInitialized = true;
                    return;
                }
                else
                {
                    Console.WriteLine("[AudioService] Linux detected. Attempting LibVLC init...");
                    try {
                        Core.Initialize();
                        _libVLC = new LibVLC("--verbose=1", "--no-video", "--no-spu", "--no-lua"); 
                        _mediaPlayer = new MediaPlayer(_libVLC);
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"[AudioService] Linux LibVLC init failed: {ex.Message}. Audio will be disabled.");
                    }
                }
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioService] Fallback Init Error: {ex.Message}");
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

            int finalVolume = volume ?? (int)SettingsService.Settings.PreviewVolume;

            if (_useWindowsNative)
            {
#if WINDOWS
                WindowsNativePlayer.Play(path, startTimeMs, finalVolume);
#endif
                return;
            }

            if (_useMacNativeFallback)
            {
#if !WINDOWS
                MacNativePlayer.Play(path, startTimeMs, finalVolume); // Use SettingsService volume if not provided
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
                    _mediaPlayer.Volume = finalVolume; // Use SettingsService volume if not provided
                    
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
            if (_useWindowsNative)
            {
#if WINDOWS
                WindowsNativePlayer.Stop();
#endif
                return;
            }

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
