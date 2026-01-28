using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace Osutag.Services
{
    public class DynamicBackgroundService
    {
        private readonly List<string> _backgrounds = new();
        private static readonly string[] SupportExtensions = { ".jpg", ".jpeg", ".png" };
        private readonly Random _rng = new();

        public void ScanBackgrounds(string osuPath)
        {
            _backgrounds.Clear();
            var bgDir = Path.Combine(osuPath, "Data", "bg");
            
            if (Directory.Exists(bgDir))
            {
                var files = Directory.GetFiles(bgDir)
                    .Where(f => SupportExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();
                
                _backgrounds.AddRange(files);
            }
        }

        public string? GetRandomBackground()
        {
            if (_backgrounds.Count == 0) return null;
            return _backgrounds[_rng.Next(_backgrounds.Count)];
        }

        public string? ExtractDominantColor(string imagePath)
        {
            try
            {
                using var image = Image.Load<Rgba32>(imagePath);
                
                // Resize to 1x1 to get the average color
                image.Mutate(x => x.Resize(1, 1));
                
                var pixel = image[0, 0];
                return $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";
            }
            catch
            {
                return null;
            }
        }

        public async Task<Stream?> GetBlurredBackgroundStreamAsync(string imagePath, int blurRadius = 2)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var image = Image.Load<Rgba32>(imagePath);
                    
                    // Downscale slightly for processing speed and better "smooth" blur
                    // osu! backgrounds are often 1920x1080 or larger.
                    // We don't need full resolution for a blurred background.
                    int targetHeight = 540;
                    if (image.Height > targetHeight)
                    {
                         double ratio = (double)targetHeight / image.Height;
                         image.Mutate(x => x.Resize((int)(image.Width * ratio), targetHeight));
                    }

                    // Apply Blur
                    image.Mutate(x => x.GaussianBlur(blurRadius));
                    
                    var ms = new MemoryStream();
                    image.SaveAsJpeg(ms); // Jpeg is fine for blurred background
                    ms.Position = 0;
                    return (Stream)ms;
                });
            }
            catch
            {
                return null;
            }
        }
    }
}
