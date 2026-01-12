using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace OsuTag.Services
{
    internal class ImageProcessor
    {
        public void ProcessCover(string inputPath, string outputPath, int targetWidth, int targetHeight)
        {
            if (!File.Exists(inputPath))
                return;

            try
            {
                using var image = Image.Load(inputPath);

                // Default: Center Crop to square (1:1 ratio)
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

        public void ProcessCoverWithCrop(string inputPath, string outputPath, int cropX, int cropY, int cropSize, int targetWidth, int targetHeight)
        {
             if (!File.Exists(inputPath)) return;
             try
             {
                 using var image = Image.Load(inputPath);
                 
                 // Validate bounds
                 if (cropX < 0) cropX = 0;
                 if (cropY < 0) cropY = 0;
                 if (cropSize <= 0) cropSize = 100;
                 if (cropX + cropSize > image.Width) cropSize = image.Width - cropX;
                 if (cropY + cropSize > image.Height) cropSize = image.Height - cropY;

                 image.Mutate(x => x
                     .Crop(new Rectangle(cropX, cropY, cropSize, cropSize))
                     .Resize(targetWidth, targetHeight)
                 );
                 
                 image.SaveAsJpeg(outputPath);
             }
             catch { }
        }
    }
}
