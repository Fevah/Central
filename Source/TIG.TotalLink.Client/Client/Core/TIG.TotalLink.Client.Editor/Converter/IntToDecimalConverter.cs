using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts an Integer or Long value to a Decimal.
    /// This will only work if the target property type is not object.
    /// </summary>
    public class IntToDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int || value is long)
                return System.Convert.ToDecimal(value);

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null; 
            
            if (value is decimal)
            {
                if (targetType == typeof(int) || targetType == typeof(int?))
                    return System.Convert.ToInt32(value);

                if (targetType == typeof(long) || targetType == typeof(long?))
                    return System.Convert.ToInt64(value);
            }

            return System.Convert.ToInt32(value);
        }
    }
}
