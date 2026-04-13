using System;
using System.Globalization;
using System.Windows.Data;
using DevExpress.Data.Async.Helpers;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Core.Converter
{
    /// <summary>
    /// Extracts the real entity from a ReadonlyThreadSafeProxyForObjectFromAnotherThread.
    /// </summary>
    [ValueConversion(typeof(ReadonlyThreadSafeProxyForObjectFromAnotherThread), typeof(object))]
    public class ThreadSafeProxyToObjectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DataModelHelper.GetDataObject(value);
        }
    }
}
