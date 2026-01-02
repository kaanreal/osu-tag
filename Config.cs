using System;
using System.IO;

namespace OsuTag
{
    /// <summary>
    /// Application configuration settings.
    /// </summary>
    public class Config
    {
        public string OsuSongsDir { get; set; }
        public string OutputDir { get; set; }
        public (int Width, int Height) SpotifyCoverSize { get; set; }

        public Config(string? outputPath = null)
        {
            OsuSongsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "osu!",
                "Songs"
            );

            OutputDir = !string.IsNullOrEmpty(outputPath) 
                ? outputPath 
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

            SpotifyCoverSize = (3000, 3000);
        }
    }
}
