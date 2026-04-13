using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts an integer into a string which describes a count of items.
    /// </summary>
    [ValueConversion(typeof(int), typeof(string))]
    public class FormatCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int || value is long)
                return string.Format("[{0}]", value);

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotSupportedException("FormatCountConverter can only be used for one way conversion.");
        }
    }
}
