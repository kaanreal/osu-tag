using System;
using System.IO;

namespace OsuTag
{
    public class Config
    {
        public string OsuSongsDir { get; set; }
        public string OutputDir { get; set; }
        public int CoverWidth { get; set; } = 3000;
        public int CoverHeight { get; set; } = 3000;

        public Config(string? outputDir = null)
        {
            // Default osu! songs directory based on platform
            OsuSongsDir = Services.PlatformService.GetDefaultOsuSongsPath();

            OutputDir = outputDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                "OsuTag"
            );
        }
    }
}
