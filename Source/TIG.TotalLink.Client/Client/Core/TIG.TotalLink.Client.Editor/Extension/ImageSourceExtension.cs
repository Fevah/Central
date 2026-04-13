using System.Collections;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TIG.TotalLink.Client.Editor.Extension
{
    public static class ImageSourceExtension
    {
        /// <summary>
        /// Returns an ImageSource as a byte array.
        /// </summary>
        /// <param name="imageSource">The ImageSource to convert to bytes.</param>
        public static byte[] GetBytes(this ImageSource imageSource)
        {
            return GetBytes(imageSource, new PngBitmapEncoder());
        }

        /// <summary>
        /// Returns an ImageSource as a byte array.
        /// </summary>
        /// <param name="imageSource">The ImageSource to convert to bytes.</param>
        /// <param name="encoder">The BitmapEncoder to use.</param>
        public static byte[] GetBytes(this ImageSource imageSource, BitmapEncoder encoder)
        {
            // Attempt to get the ImageSource as a BitmapSource
            var bitmapSource = imageSource as BitmapSource;
            if (bitmapSource == null)
                return null;

            // Add the bitmap to the encoder
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            // Extract the bytes from the encoder
            byte[] bytes = null;
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                bytes = stream.ToArray();
            }

            // Return the bytes
            return bytes;
        }
    }
}
