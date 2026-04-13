using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Editors;
using MonitoredUndo;

namespace TIG.TotalLink.Client.Converter
{
    public class ListViewMouseMoveEventArgsConverter : EventArgsConverterBase<MouseEventArgs>
    {
        protected override object Convert(object sender, MouseEventArgs args)
        {
            // Abort if the sender is not a ListBoxEdit
            var listBoxeEdit = sender as ListBoxEdit;
            if (listBoxeEdit == null) return null;

            // Abort if the itemsSource of the ListBoxEdit is not an ICollectionView
            var source = listBoxeEdit.ItemsSource as ICollectionView;
            if (source == null) return null;

            // Get the item under the mouse
            var pt = args.GetPosition(listBoxeEdit);
            var item = System.Windows.Media.VisualTreeHelper.HitTest(listBoxeEdit, pt);

            // Abort if the there is no item under the mouse, or the control under the mouse is not a changeset
            if (item == null) return null;
            var contentPresenter = System.Windows.Media.VisualTreeHelper.GetParent(item.VisualHit) as ContentPresenter;
            if (contentPresenter == null) return null;
            var changeSet = contentPresenter.Content as ChangeSet;
            if (changeSet == null) return null;

            // Return the index of the item in the itemsSource
            return source.SourceCollection.Cast<ChangeSet>().ToList().IndexOf(changeSet);
        }
    }
}
