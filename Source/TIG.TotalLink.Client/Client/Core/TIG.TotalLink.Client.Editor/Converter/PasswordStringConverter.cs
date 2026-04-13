using System;
using System.Globalization;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Returns a string with all characters replaced by the password character.
    /// </summary>
    [ValueConversion(typeof(string), typeof(string))]
    public class PasswordStringConverter : IValueConverter
    {
        private const char PasswordChar = '●';

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Attempt to get the value as a string
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Return a new string made of password characters
            return new string(PasswordChar, stringValue.Length);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotSupportedException("PasswordStringConverter can only be used for one way conversion.");
        }
    }
}
