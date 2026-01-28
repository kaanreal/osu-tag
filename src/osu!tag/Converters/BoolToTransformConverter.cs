using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Osutag.Converters
{
    public class BoolToTransformConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool b) boolValue = b;
            else if (value is int i) boolValue = i > 0;
            else if (value is double d) boolValue = d > 0;

            if (parameter is string paramStr)
            {
                var parts = paramStr.Split(',');
                if (parts.Length >= 2)
                {
                    string transformStr = boolValue ? parts[1] : parts[0];
                    return Avalonia.Media.Transformation.TransformOperations.Parse(transformStr);
                }
            }
            return Avalonia.Media.Transformation.TransformOperations.Parse("none");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
