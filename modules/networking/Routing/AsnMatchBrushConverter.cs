using System.Globalization;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Central.Module.Networking.Routing;

/// <summary>
/// true (ASN matches expected) → green
/// false (mismatch) → red — needs attention
/// </summary>
public sealed class AsnMatchBrushConverter : System.Windows.Data.IValueConverter
{
    private static readonly System.Windows.Media.Brush _match    = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)); // green
    private static readonly System.Windows.Media.Brush _mismatch = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // red

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? _match : _mismatch;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
