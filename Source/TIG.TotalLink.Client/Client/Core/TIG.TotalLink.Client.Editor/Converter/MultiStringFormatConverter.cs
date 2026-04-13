using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Formats a value.
    /// Use this when the format is dynamic and you need to bind it to a property.
    /// parameter = If true, value[1] will be treated as a complete format string (e.g. "{0:c}") instead of just the pattern (e.g. "c")
    /// Value[0] = The value to convert.
    /// Value[1] = A string that specifies how to format the input value.
    /// </summary>
    [ValueConversion(typeof(object), typeof(string))]
    public class MultiStringFormatConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Abort if the input value or format string are null
            var inputValue = values.Length > 0 ? values[0] : null;
            var formatString = values.Length > 1 ? values[1] as string : null;
            if (inputValue == null || formatString == null)
                return null;

            // Determine if a full format string has been supplied
            bool fullFormatSupplied;
            bool.TryParse(parameter as string, out fullFormatSupplied);

            // If a full format string was not supplied, convert the pattern to a format string
            if (!fullFormatSupplied)
                formatString = string.Format("{{0:{0}}}", formatString);

            // Return the formatted value
            return string.Format(formatString, inputValue);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("MultiStringFormatConverter can only be used for one way conversion.");
        }
    }
}
