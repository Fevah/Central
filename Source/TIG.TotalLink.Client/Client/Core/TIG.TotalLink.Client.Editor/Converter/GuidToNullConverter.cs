using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Returns a Guid value as null when it contains Guid.Empty.
    /// </summary>
    [ValueConversion(typeof(Guid), typeof(Guid))]
    public class GuidToNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Guid && ((Guid)value).Equals(Guid.Empty))
                return null;

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Guid.Empty;

            return value;
        }
    }
}
