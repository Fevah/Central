using System.Xml;

namespace TIG.TotalLink.Client.Core.Extension
{
    public static class XmlReaderExtension
    {

        /// <summary>
        /// Reads a boolean attribute value.
        /// </summary>
        /// <param name="xml">The XmlReader to collect the value from.</param>
        /// <param name="name">The name of the attribute to collect.</param>
        /// <param name="defaultValue">The default value to return if the attribute does not exist.</param>
        /// <returns>The boolean value read from the attribute.</returns>
        public static bool ReadAttributeAsBoolean(this XmlReader xml, string name, bool defaultValue)
        {
            bool value;
            if (bool.TryParse(xml.GetAttribute(name), out value))
                return value;

            return defaultValue;
        }
    }
}
