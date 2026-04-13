using System;
using System.Reflection;

namespace TIG.TotalLink.Shared.DataModel.Core.Extension
{
    public static class PropertyInfoExtension
    {
        /// <summary>
        /// Sets an int property using a string value.
        /// </summary>
        /// <param name="property">The property to set.</param>
        /// <param name="obj">The object whose property will be set.</param>
        /// <param name="value">The new property value.</param>
        public static void SetIntValue(this PropertyInfo property, object obj, string value)
        {
            int i;
            if (int.TryParse(value, out i))
                property.SetValue(obj, i);
        }

        /// <summary>
        /// Sets a decimal property using a string value.
        /// </summary>
        /// <param name="property">The property to set.</param>
        /// <param name="obj">The object whose property will be set.</param>
        /// <param name="value">The new property value.</param>
        public static void SetDecimalValue(this PropertyInfo property, object obj, string value)
        {
            decimal d;
            if (decimal.TryParse(value, out d))
                property.SetValue(obj, d);
        }

        /// <summary>
        /// Sets a bool property using a string value.
        /// </summary>
        /// <param name="property">The property to set.</param>
        /// <param name="obj">The object whose property will be set.</param>
        /// <param name="value">The new property value.</param>
        public static void SetBoolValue(this PropertyInfo property, object obj, string value)
        {
            bool b;
            if (bool.TryParse(value, out b))
                property.SetValue(obj, b);
        }

        /// <summary>
        /// Sets a guid property using a string value.
        /// </summary>
        /// <param name="property">The property to set.</param>
        /// <param name="obj">The object whose property will be set.</param>
        /// <param name="value">The new property value.</param>
        public static void SetGuidValue(this PropertyInfo property, object obj, string value)
        {
            Guid g;
            if (Guid.TryParse(value, out g))
                property.SetValue(obj, g);
        }
    }
}
