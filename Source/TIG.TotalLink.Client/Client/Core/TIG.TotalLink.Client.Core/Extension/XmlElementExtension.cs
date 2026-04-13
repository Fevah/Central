using System.Xml;

namespace TIG.TotalLink.Client.Core.Extension
{
    public static class XmlElementExtension
    {
        /// <summary>
        /// Gets an attribute value converted to a Boolean.
        /// </summary>
        /// <param name="element">The element to collect the attribute value from.</param>
        /// <param name="name">The name of the attribute to collect the value from.</param>
        /// <returns>The attribute value converted to a Boolean.</returns>
        public static bool GetBoolAttribute(this XmlElement element, string name)
        {
            var stringValue = element.GetAttribute(name);

            bool b;
            bool.TryParse(stringValue, out b);
            return b;
        }

        /// <summary>
        /// Gets an attribute value converted to an Enum.
        /// </summary>
        /// <typeparam name="TEnum">The type of enum to convert the value to.</typeparam>
        /// <param name="element">The element to collect the attribute value from.</param>
        /// <param name="name">The name of the attribute to collect the value from.</param>
        /// <returns>The attribute value converted to a <typeparamref name="TEnum"/>.</returns>
        public static TEnum GetEnumAttribute<TEnum>(this XmlElement element, string name)
            where TEnum : struct
        {
            var stringValue = element.GetAttribute(name);

            TEnum e;
            System.Enum.TryParse(stringValue, out e);
            return e;
        }
    }
}
