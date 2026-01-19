using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Osutag.Services
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
        // Windows Native Player using winmm.dll (mciSendString)
        // This is lightweight and avoids COM threading issues associated with WMP/OCX
        internal static class WindowsNativePlayer
        {
            [DllImport("winmm.dll")]
            private static extern long mciSendString(string strCommand, StringBuilder? strReturn, int iReturnLength, IntPtr hwndCallback);

            [DllImport("winmm.dll")]
            private static extern int mciGetErrorString(int errCode, StringBuilder strReturn, int iReturnLength);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern uint GetShortPathName([MarshalAs(UnmanagedType.LPTStr)] string lpszLongPath, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszShortPath, uint cchBuffer);

            public static void Play(string path, int startTimeMs, float volume)
            {
                // Stop any previous playback first to be safe
                Stop();

                try
                {
                    // Convert to short path to avoid quote/space issues in MCI commands
                    StringBuilder shortPath = new StringBuilder(255);
                    GetShortPathName(path, shortPath, (uint)shortPath.Capacity);
                    string playPath = shortPath.ToString();

                    if (string.IsNullOrEmpty(playPath))
                    {
                        // Fallback to manual quoting if short path fails
                        playPath = $"\"{path}\"";
                    }

                    // Open the file
                    string alias = "osutag_preview";
                    
                    // Close just in case
                    mciSendString($"close {alias}", null, 0, IntPtr.Zero);

                    string openCommand = $"open {playPath} type mpegvideo alias {alias}";
                    int result = (int)mciSendString(openCommand, null, 0, IntPtr.Zero);

                    if (result != 0)
                    {
                        StringBuilder sb = new StringBuilder(128);
                        mciGetErrorString(result, sb, 128);
                        return;
                    }

                    // Set volume (0-1000)
                    int mciVolume = (int)(volume * 10);
                    mciSendString($"setaudio {alias} volume to {mciVolume}", null, 0, IntPtr.Zero);

                    // Seek if needed
                    if (startTimeMs > 0)
                    {
                        mciSendString($"seek {alias} to {startTimeMs}", null, 0, IntPtr.Zero);
                    }

                    // Play
                    mciSendString($"play {alias}", null, 0, IntPtr.Zero);
                }
                catch (Exception)
                {
                }
            }

            public static void Stop()
            {
                try
                {
                    string alias = "osutag_preview";
                    mciSendString($"stop {alias}", null, 0, IntPtr.Zero);
                    mciSendString($"close {alias}", null, 0, IntPtr.Zero);
                }
                catch { }
            }

            public static void Dispose()
            {
                Stop();
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

            internal static void Dispose()
            {
                throw new NotImplementedException();
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

            if (PlatformService.IsWindows)
            {
                _useWindowsNative = true; 
            }
            else if (PlatformService.IsMacOS)
            {
                _useMacNative = true;
            }
            
            _isInitialized = true;
        }

        public void PlayPreview(string path, int startTimeMs, int? volume = null)
        {
            if (!_isInitialized) Initialize();

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
                    return;
                }

                // Prevent playing the same file if already playing
                lock (_playbackLock)
                {
                    if (_currentPlayingPath == path)
                    {
                        return;
                    }
                    _currentPlayingPath = path;
                }

                int finalVolume = volume ?? _volume;

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
            }
            catch (TaskCanceledException) { }
            catch (Exception) { }
        }

        public void Stop()
        {
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
            
            if (_useWindowsNative)
            {
#if WINDOWS
                WindowsNativePlayer.Dispose();
#endif
            }
            else if (_useMacNative)
            {
#if !WINDOWS
                MacNativePlayer.Dispose();
#endif
            }
        }
    }
}
