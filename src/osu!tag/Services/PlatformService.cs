using System;
using System.Runtime.InteropServices;

namespace Osutag.Services
{
    /// <summary>
    /// Cross-platform service for detecting OS and providing platform-specific paths.
    /// </summary>
    public static class PlatformService
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// Gets the platform name for telemetry and display purposes.
        /// </summary>
        public static string GetPlatformName()
        {
            if (IsWindows) return "Windows";
            if (IsMacOS) return "macOS";
            if (IsLinux) return "Linux";
            return "Unknown";
        }

        /// <summary>
        /// Gets the default osu! Songs folder path for the current platform.
        /// </summary>
        public static string GetDefaultOsuSongsPath()
        {
            if (IsWindows)
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "osu!",
                    "Songs"
                );
            }
            else if (IsMacOS)
            {
                // osu! on macOS typically installs here
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return System.IO.Path.Combine(home, "Library", "Application Support", "osu!", "Songs");
            }
            else if (IsLinux)
            {
                // osu! on Linux (via Wine or native osu!lazer)
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                // Try osu!lazer path first
                var lazerPath = System.IO.Path.Combine(home, ".local", "share", "osu", "Songs");
                if (System.IO.Directory.Exists(lazerPath))
                    return lazerPath;
                // Fallback to Wine path
                return System.IO.Path.Combine(home, ".wine", "drive_c", "users", Environment.UserName, "AppData", "Local", "osu!", "Songs");
            }
            
            return "";
        }

        /// <summary>
        /// Gets the default Companella database path for the current platform.
        /// </summary>
        public static string GetDefaultCompanellaPath()
        {
            if (IsWindows)
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "companella"
                );
            }
            else if (IsMacOS)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return System.IO.Path.Combine(home, "Library", "Application Support", "companella");
            }
            else if (IsLinux)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return System.IO.Path.Combine(home, ".config", "companella");
            }
            
            return "";
        }
    }
}
