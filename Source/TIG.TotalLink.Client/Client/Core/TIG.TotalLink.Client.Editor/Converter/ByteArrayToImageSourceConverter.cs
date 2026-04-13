using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TIG.TotalLink.Client.Editor.Extension;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts a byte array to an ImageSource.
    /// </summary>
    [ValueConversion(typeof(byte[]), typeof(ImageSource))]
    public class ByteArrayToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var bytes = value as byte[];
            if (bytes != null)
                return new ImageSourceConverter().ConvertFrom(bytes) as ImageSource;

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var imageSource = value as ImageSource;
            if (imageSource != null)
                return imageSource.GetBytes();

            return null;
        }
    }
}
