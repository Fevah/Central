using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Used to bind a property to the return value of a method.
    /// ConverterParameter  = Name of the method to call.
    /// Value[0]            = Target object containing the method to call.
    /// Value[1..x]         = Parameter values to pass to the method.
    /// </summary>
    public class MultiMethodToValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Abort if the target or method name are null
            var target = values.Length > 0 ? values[0] : null;
            var methodName = parameter as string;
            if (target == null || methodName == null)
                return null;

            // Create arrays of the parameters and their types
            var parameters = values.Skip(1).ToArray();
            var parameterTypes = values.Skip(1).Where(v => v != null).Select(v => v.GetType()).ToArray();

            // If the parameters and parameterTypes arrays are different lengths, then some of the parameters were null so we can't determine the types
            // Therefore we have to abort
            if (parameters.Length != parameterTypes.Length)
                return null;

            // Attempt to get the method to call
            var method = target.GetType().GetMethod(methodName, parameterTypes);
            if (method == null)
                return null;

            // Invoke the method and return the result
            return method.Invoke(target, parameters);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("MultiMethodToValueConverter can only be used for one way conversion.");
        }
    }
}
