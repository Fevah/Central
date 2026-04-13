using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts an Int32 value which describes a number of minutes into a Decimal that describes the number of hours rounded to 2 decimal places.
    /// </summary>
    [ValueConversion(typeof(int), typeof(decimal))]
    public class MinuteToHourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If value is not a valid int, return 0h
            if (!(value is int))
            {
                return "0h";
            }

            // Return the value converted from minutes to hours, and appended with "h"
            return string.Format("{0}h", Math.Round(System.Convert.ToDecimal(value) / 60m, 2));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Try to convert the display value to a decimal, and convert it from hours to minutes
            decimal hour;
            if (decimal.TryParse(value.ToString().TrimEnd('h'), out hour))
            {
                return (int)(hour * 60m);
            }

            // If the conversion failed, return 0
            return 0;
        }
    }
}
