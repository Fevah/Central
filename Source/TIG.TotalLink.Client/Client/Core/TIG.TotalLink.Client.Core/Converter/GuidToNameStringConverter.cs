using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Core.Converter
{
    /// <summary>
    /// Converts a Guid value to a string that can be used as a valid control name.
    /// </summary>
    [ValueConversion(typeof(Guid), typeof(string))]
    public class GuidToNameStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Guid)
                return string.Format("_{0}", ((Guid)value).ToString("N"));

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Guid guid;
            var s = value as string;
            if (s == null || !s.StartsWith("_"))
                return Guid.Empty;

            s = s.Remove(0, 1);
            if (Guid.TryParse(s, out guid))
                return guid;

            return Guid.Empty;
        }
    }
}
