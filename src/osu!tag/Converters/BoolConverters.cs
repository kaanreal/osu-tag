using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Osutag.Converters
{
    /// <summary>
    /// Converts a boolean to a string value.
    /// Pass "TrueValue,FalseValue" as the parameter.
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramStr)
            {
                var parts = paramStr.Split(',');
                if (parts.Length >= 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }
            return value?.ToString() ?? "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean to an expand/collapse icon.
    /// </summary>
    public class ExpandIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "▼" : "▶";
            }
            return "▶";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean to its inverse for IsVisible binding.
    /// In Avalonia, we use IsVisible (bool) instead of Visibility (enum).
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    /// <summary>
    /// Converts an integer to a boolean for IsVisible binding.
    /// Returns true if value > 0.
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts null to false, non-null to true for IsVisible binding.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a numeric value to a boolean. Returns true if > 0.
    /// </summary>
    public class NumericToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue) return intValue > 0;
            if (value is double doubleValue) return doubleValue > 0;
            if (value is long longValue) return longValue > 0;
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean to a double height value.
    /// Pass "FalseHeight,TrueHeight" as the parameter.
    /// </summary>
    public class BoolToHeightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramStr)
            {
                var parts = paramStr.Split(',');
                if (parts.Length >= 2 && double.TryParse(boolValue ? parts[1] : parts[0], out double height))
                {
                    return height;
                }
            }
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean to an icon string based on parameter "TrueIcon,FalseIcon".
    /// </summary>
    public class BoolToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramStr)
            {
                var parts = paramStr.Split(',');
                if (parts.Length >= 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Converts a string to a boolean. Returns true if not null or whitespace.
    /// Parameter "Inverse" returns true if null or whitespace.
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasValue = !string.IsNullOrWhiteSpace(value as string);
            if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                return !hasValue;
            }
            return hasValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean to "Linked" or "Normal" text.
    /// </summary>
    public class BoolToLinkedTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? "Linked" : "Normal";
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a boolean to AccentBrush (true) or TextPrimaryBrush (false).
    /// Caches brush references to avoid repeated resource lookups.
    /// </summary>
    public class BoolToLinkedColorConverter : IValueConverter
    {
        // Cached brush references (initialized on first use)
        private static object? _accentBrush;
        private static object? _textPrimaryBrush;
        private static bool _cachesInitialized;

        private static void EnsureCachesInitialized()
        {
            if (_cachesInitialized) return;
            
            if (Avalonia.Application.Current?.Resources != null)
            {
                Avalonia.Application.Current.Resources.TryGetResource("AccentBrush", Avalonia.Styling.ThemeVariant.Default, out _accentBrush);
                Avalonia.Application.Current.Resources.TryGetResource("TextPrimaryBrush", Avalonia.Styling.ThemeVariant.Default, out _textPrimaryBrush);
                _cachesInitialized = true;
            }
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            EnsureCachesInitialized();
            return (value is bool b && b) ? _accentBrush : _textPrimaryBrush;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
