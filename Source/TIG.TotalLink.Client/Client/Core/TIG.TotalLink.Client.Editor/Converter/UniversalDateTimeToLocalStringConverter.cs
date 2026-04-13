using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts a universal DateTime value to a local formatted string.
    /// </summary>
    public class UniversalDateTimeToLocalStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dateTimeValue = DateTime.MinValue;

            // Get the DateTime if the source value is a string
            if (value is string)
                DateTime.TryParse(value.ToString(), out dateTimeValue);

            // Get the DateTime if the source value is a DateTime
            if (value is DateTime)
                dateTimeValue = (DateTime)value;

            // If no valid DateTime was found, return null
            if (dateTimeValue == DateTime.MinValue)
                return null;

            // Return the formatted DateTime converted to local time
            return dateTimeValue.ToLocalTime().ToString(string.Format("{0} {1}", CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern, CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotSupportedException("UniversalDateTimeToLocalStringConverter can only be used for one way conversion.");
        }
    }
}
