using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Forms;
using TIG.TotalLink.Client.Core.Extension;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts an RTF string into plain text.
    /// </summary>
    [ValueConversion(typeof(string), typeof(string))]
    public class RtfToStringConverter : IValueConverter
    {
        private static readonly RichTextBox RichTextBox = new RichTextBox();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Attempt to get the value as a string
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Abort if the string does not appear to be rtf
            stringValue = stringValue.Trim();
            if (!(stringValue.StartsWith("{\\rtf1") && stringValue.EndsWith("}")))
                return null;

            // Attempt to convert the rich text value to plain text
            try
            {
                RichTextBox.Rtf = stringValue;
                var plainText = RichTextBox.Text.Trim().RemoveTabs().ReplaceNewlines().NormalizeSpaces();
                RichTextBox.Rtf = null;
                return plainText;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("RtfToStringConverter can only be used for one way conversion.");
        }
    }
}
