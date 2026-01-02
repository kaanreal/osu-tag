using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace OsuTag.Services
{
    public class ImageProcessor
    {
        public void ProcessCover(string inputPath, string outputPath, int targetWidth, int targetHeight)
        {
            if (!File.Exists(inputPath))
                return;

            try
            {
                using var image = Image.Load(inputPath);
                
                // Crop to square (1:1 ratio)
                int minDim = Math.Min(image.Width, image.Height);
                int left = (image.Width - minDim) / 2;
                int top = (image.Height - minDim) / 2;

                image.Mutate(x => x
                    .Crop(new Rectangle(left, top, minDim, minDim))
                    .Resize(targetWidth, targetHeight)
                );

                image.SaveAsJpeg(outputPath);
            }
            catch
            {
                // Cover processing is optional - silently skip on error
            }
        }
    }
}
