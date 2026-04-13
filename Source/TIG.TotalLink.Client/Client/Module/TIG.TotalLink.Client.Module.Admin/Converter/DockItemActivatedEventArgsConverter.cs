using System.Windows;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Docking;
using DevExpress.Xpf.Docking.Base;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.Converter
{
    public class DockItemActivatedEventArgsConverter : EventArgsConverterBase<DockItemActivatedEventArgs>
    {
        protected override object Convert(object sender, DockItemActivatedEventArgs args)
        {
            // Attempt to get the item as a ContentItem
            var contentItem = args.Item as ContentItem;
            if (contentItem == null)
                return null;

            // Attempt to get the item content as a FrameworkElement
            var frameworkElement = contentItem.Content as FrameworkElement;
            if (frameworkElement == null)
                return null;

            // Return the content DataContext as a DocumentViewModel
            return frameworkElement.DataContext as DocumentViewModel;
        }
    }
}
