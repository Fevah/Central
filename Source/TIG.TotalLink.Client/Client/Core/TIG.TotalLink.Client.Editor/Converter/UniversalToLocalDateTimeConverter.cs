using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts a DateTime value from universal time to local time and back again.
    /// </summary>
    [ValueConversion(typeof(DateTime), typeof(DateTime))]
    public class UniversalToLocalDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime)
                return ((DateTime)value).ToLocalTime();

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime)
                return ((DateTime)value).ToUniversalTime();

            return null;
        }
    }
}
