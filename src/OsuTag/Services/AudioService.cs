using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
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

        public int Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                // Volume is applied per-playback in native players
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
                    try
                    {
                        StopInternal();
                        
                        string shortPath = GetShortPathName(path);
                        
                        string[] variations = {
                            $"open \"{shortPath}\" type mpegvideo alias preview",
                            $"open \"{shortPath}\" alias preview",
                            $"open \"{path}\" type mpegvideo alias preview",
                            $"open \"{path}\" alias preview"
                        };

                        long res = -1;
                        foreach (var cmd in variations)
                        {
                            try {
                                res = mciSendString(cmd, null, 0, IntPtr.Zero);
                                if (res == 0) break;
                            } catch { }
                        }

                        if (res == 0)
                        {
                            mciSendString("set preview time format ms", null, 0, IntPtr.Zero);
                            int vol = (int)Math.Clamp(volume * 10, 0, 1000);
                            mciSendString("setaudio preview volume to " + vol, null, 0, IntPtr.Zero);
                            mciSendString("play preview from " + startTimeMs, null, 0, IntPtr.Zero);
                        }
                    }
                    catch { }
                });
            }

            public static void Stop() => Task.Run(() => { try { StopInternal(); } catch { } });

            private static void StopInternal()
            {
                mciSendString("stop preview", null, 0, IntPtr.Zero);
                mciSendString("close preview", null, 0, IntPtr.Zero);
            }

            private static string GetShortPathName(string path)
            {
                try
                {
                    StringBuilder shortPath = new StringBuilder(1024);
                    int res = GetShortPathName(path, shortPath, shortPath.Capacity);
                    return res > 0 ? shortPath.ToString() : path;
                }
                catch { return path; }
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

        public void Stop()
        {
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
