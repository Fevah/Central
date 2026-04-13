using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts a Long value to a Decimal.
    /// </summary>
    public class LongToDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int || value is long)
                return System.Convert.ToDecimal(value);

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal)
                return System.Convert.ToInt64(value);

            return null;
        }
    }
}
