using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Central.Module.Links.Views;

/// <summary>
/// Converts a boolean match result to a foreground brush:
///   true  → default text (inherited) — description matches live switch
///   false → orange (#D97706) — needs updating on the switch
/// Used by the B2B description columns to flag stale descriptions.
/// </summary>
public sealed class MatchBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush _match    = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.Brush _mismatch = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x06)); // amber

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? _match : _mismatch;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
