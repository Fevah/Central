using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts a universal DateTime value to a local formatted string.
    /// Value[0] = The DateTime value to convert.
    /// Value[1] = A string that specifies how to format the DateTime.
    /// </summary>
    public class MultiUniversalDateTimeToLocalStringConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var dateTimeValue = DateTime.MinValue;

            // Get the DateTime if the source value is a string
            if (values[0] is string)
                DateTime.TryParse(values[0].ToString(), out dateTimeValue);

            // Get the DateTime if the source value is a DateTime
            if (values[0] is DateTime)
                dateTimeValue = (DateTime)values[0];

            // If no valid DateTime was found, return null
            if (dateTimeValue == DateTime.MinValue)
                return null;

            // Attempt to get the format
            var format = values.Length > 1 ? values[1].ToString() : null;
            if (format == null)
            {
                // Return the DateTime converted to local time with a default format
                return dateTimeValue.ToLocalTime().ToString(string.Format("{0} {1}", CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern, CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern));
            }

            // Return the DateTime converted to local time with the specified format
            return dateTimeValue.ToLocalTime().ToString(format);
        }

        public object[] ConvertBack(object values, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("MultiUniversalDateTimeToLocalStringConverter can only be used for one way conversion.");
        }
    }
}
