using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Joins multiple string values.
    /// ConverterParameter = Specifies an optional delimeter to add between the strings.  Defaults to ", ".
    /// </summary>
    [ValueConversion(typeof(string), typeof(string), ParameterType = typeof(string))]
    public class MultiStringConcatenationConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var sb = new StringBuilder();
            var delimiter = (parameter != null ? parameter.ToString() : ", ");

            // Concatenate all fields
            foreach (var value in values)
            {
                var valueString = value != null ? value.ToString() : null;
                if (!string.IsNullOrWhiteSpace(valueString))
                {
                    if (sb.Length > 0)
                        sb.Append(delimiter);
                    sb.Append(valueString);
                }
            }

            return sb.ToString();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("MultiStringConcatenationConverter can only be used for one way conversion.");
        }

    }
}
