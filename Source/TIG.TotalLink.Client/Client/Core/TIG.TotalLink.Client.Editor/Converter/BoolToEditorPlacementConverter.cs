using System;
using System.Globalization;
using System.Windows.Data;
using DevExpress.Xpf.Editors;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts a Boolean value to an EditorPlacement.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(EditorPlacement))]
    public class BoolToEditorPlacementConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool)
                return (bool)value ? EditorPlacement.EditBox : EditorPlacement.None;

            return EditorPlacement.None;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EditorPlacement)
                return (EditorPlacement)value != EditorPlacement.None;

            return false;
        }
    }
}
