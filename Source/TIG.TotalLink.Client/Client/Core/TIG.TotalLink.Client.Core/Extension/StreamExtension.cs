using System.IO;
using System.Text;
using System.Xml;

namespace TIG.TotalLink.Client.Core.Extension
{
    public static class StreamExtension
    {
        /// <summary>
        /// Gets a stream as an string using UTF8 encoding.
        /// </summary>
        /// <param name="stream">The Stream to read string data from.</param>
        /// <returns>The stream as a string.</returns>
        public static string GetAsUtf8String(this Stream stream)
        {
            return GetAsString(stream, Encoding.UTF8);
        }

        /// <summary>
        /// Gets a stream as a string using the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to read string data from.</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <returns>The stream as a string.</returns>
        public static string GetAsString(this Stream stream, Encoding encoding)
        {
            // Position the stream to the beginning
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            // Read the string from the stream
            var reader = encoding != null ? new StreamReader(stream, encoding) : new StreamReader(stream);
            var s = reader.ReadToEnd();

            // Reposition the stream to the beginning
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            // Return the string
            return s;
        }

        /// <summary>
        /// Stores a string in a stream using UTF8 encoding.
        /// </summary>
        /// <param name="stream">The stream to write the string to.</param>
        /// <param name="s">The string to write.</param>
        public static void SetAsUtf8String(this Stream stream, string s)
        {
            SetAsString(stream, s, Encoding.UTF8);
        }

        /// <summary>
        /// Stores a string in a stream using the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to write the string to.</param>
        /// <param name="s">The string to write.</param>
        /// <param name="encoding">The character encoding to use.</param>
        public static void SetAsString(this Stream stream, string s, Encoding encoding)
        {
            // Clear the stream
            stream.SetLength(0);

            // Position the stream to the beginning
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            // Write the string to the stream
            var writer = new XmlTextWriter(stream, encoding);
            writer.WriteRaw(s);
            writer.Flush();

            // Reposition the stream to the beginning
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);
        }
    }
}
