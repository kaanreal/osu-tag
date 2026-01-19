using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Osutag.Converters
{
    public class PathToBitmapConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        using var stream = System.IO.File.OpenRead(path);
                        // Decode to width 400 (higher quality thumbnail) to save memory/performance
                        return Bitmap.DecodeToWidth(stream, 400);
                    }
                }
                catch
                {
                    // Return null to show nothing or fallback
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
