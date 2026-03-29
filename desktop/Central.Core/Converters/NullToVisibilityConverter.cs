using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Central.Core.Converters;

/// <summary>
/// Engine converter: null/empty → one Visibility, non-null → another.
/// Used for showing/hiding image vs text fallback in icon columns.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public Visibility NullValue { get; set; } = Visibility.Collapsed;
    public Visibility NotNullValue { get; set; } = Visibility.Visible;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value == null ? NullValue : NotNullValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
