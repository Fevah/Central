using System;
using System.Collections.Generic;
using System.IO;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class DocumentLayoutHelper
    {
        #region Public Methods

        /// <summary>
        /// Converts a dictionary of panel layouts to a stream.
        /// </summary>
        /// <param name="layouts">The dictionary containing layouts to convert.</param>
        /// <returns>The dictionary of panel layouts as a stream.</returns>
        public static Stream ConvertToStream(Dictionary<Guid, string> layouts)
        {
            // Abort if the layout dictionary is null or empty
            if (layouts == null || layouts.Count == 0)
                return null;

            // Create a new MemoryStream and BinaryWriter
            var layoutStream = new MemoryStream();
            var writer = new BinaryWriter(layoutStream);

            // Write the count of items in the dictionary
            writer.Write(layouts.Count);

            // Write each layout
            foreach (var kvp in layouts)
            {
                //writer.Write(GuidToNameStringConverter.Convert(kvp.Key, typeof(string), null, CultureInfo.CurrentCulture).ToString());
                writer.Write(kvp.Key.ToString());
                writer.Write(kvp.Value);
            }
            writer.Flush();

            // Reposition the stream to the beginning
            layoutStream.Seek(0, SeekOrigin.Begin);

            // Return the stream
            return layoutStream;
        }

        /// <summary>
        /// Converts a stream to a dictionary of panel layouts.
        /// </summary>
        /// <param name="layout">The stream containing layouts to convert.</param>
        /// <param name="closeStream">Indicates if the stream should be closed.</param>
        /// <returns>The stream as a dictionary of panel layouts.</returns>
        public static Dictionary<Guid, string> ConvertFromStream(Stream layout, bool closeStream = true)
        {
            // Abort if no layout was supplied
            if (layout == null)
                return null;

            // Create a new BinaryReader
            var reader = new BinaryReader(layout);

            // Read the count of layouts and create a dictionary to contain them
            var count = reader.ReadInt32();
            var layouts = new Dictionary<Guid, string>(count);

            // Read each layout into the dictionary
            for (var i = 0; i < count; i++)
            {
                //var layoutKey = (Guid)GuidToNameStringConverter.ConvertBack(reader.ReadString(), typeof(Guid), null, CultureInfo.CurrentCulture);
                var layoutKey = Guid.Parse(reader.ReadString());
                var layoutString = reader.ReadString();
                layouts.Add(layoutKey, layoutString);
            }

            if (closeStream)
            {
                // Close the stream
                reader.Dispose();
            }
            else
            {
                // Reposition the stream to the beginning
                if (layout.CanSeek)
                    layout.Seek(0, SeekOrigin.Begin);
            }

            // Return the dictionary of layouts
            return layouts;
        }

        #endregion
    }
}
