using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.LayoutControl;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Returns the IsEnabled property of the LayoutGroupEx that is associated with a DXTabItem.
    /// </summary>
    [ValueConversion(typeof(DXTabItem), typeof(bool))]
    public class TabItemToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Attempt to get the value as a DXTabItem
            var tabItem = value as DXTabItem;
            if (tabItem != null)
            {
                // Attempt to get the parent DXTabControl
                var tabControl = tabItem.Parent as DXTabControl;
                if (tabControl == null)
                    return tabItem.IsEnabled;

                // Attempt to get the parent LayoutGroup
                var tabbedGroup = tabControl.Parent as LayoutGroup;
                if (tabbedGroup == null)
                    return tabItem.IsEnabled;

                // Find the index of the tab item, and return the IsEnabled property from the related LayoutGroup
                var tabIndex = tabControl.Items.IndexOf(tabItem);
                return tabbedGroup.GetLogicalChildren(true)[tabIndex].IsEnabled;
            }

            // Attempt to get the value as a Framework element
            var element = value as FrameworkElement;
            if (element != null)
            {
                return element.IsEnabled;
            }

            // The value was not a valid element, so just return true
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotSupportedException("TabItemToLayoutGroupConverter can only be used for one way conversion.");
        }
    }
}
