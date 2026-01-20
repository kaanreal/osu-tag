using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Osutag.Converters
{
    /// <summary>
    /// Converts a boolean to a double value.
    /// Pass "FalseValue,TrueValue" as the parameter.
    /// </summary>
    public class BoolToDoubleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool b) boolValue = b;
            else if (value is int i) boolValue = i > 0;
            else if (value is double d) boolValue = d > 0;

            string paramStr = parameter as string ?? "0,1";
            var parts = paramStr.Split(',');
            if (parts.Length >= 2)
            {
                if (double.TryParse(boolValue ? parts[1] : parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }
            }
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
