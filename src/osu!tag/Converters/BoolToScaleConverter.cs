using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Osutag.Converters
{
    public class BoolToScaleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;
            
            // Default: Scale from 0.9 to 1.0
            double scale = isVisible ? 1.0 : 0.9;
            
            if (parameter is string paramStr && double.TryParse(paramStr, out double startScale))
            {
                scale = isVisible ? 1.0 : startScale;
            }

            return new ScaleTransform(scale, scale);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
