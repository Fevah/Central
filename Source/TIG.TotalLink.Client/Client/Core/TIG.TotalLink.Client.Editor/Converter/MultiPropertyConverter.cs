using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Allows binding to a property by name.
    /// This will work on both private and public properties but will be slower than a direct binding.
    /// Value[0] = Target object that the property value will be collected from.
    /// Value[1] = Name of the property.
    /// </summary>
    public class MultiPropertyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Abort if the target or propertyName are null
            var target = values.Length > 0 ? values[0] : null;
            var propertyName = values.Length > 1 ? values[1] as string : null;
            if (target == null || propertyName == null)
                return null;

            // Attempt to get the property
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
                return null;

            // Return the value from the property
            return property.GetValue(target);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("MultiPrivatePropertyConverter can only be used for one way conversion.");
        }
    }
}
