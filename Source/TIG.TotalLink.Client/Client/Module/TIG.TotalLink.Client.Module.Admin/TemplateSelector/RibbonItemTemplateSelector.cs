using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Ribbon;

namespace TIG.TotalLink.Client.Module.Admin.TemplateSelector
{
    /// <summary>
    /// Template selector for classes that inherit RibbonItemViewModelBase.
    /// </summary>
    public class RibbonItemTemplateSelector : DataTemplateSelector
    {
        #region Overrides

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null)
                return null;

            // Find the BarManager that this item belongs to
            BarManager manager = null;

            var ribbonGroup = container as RibbonPageGroup;
            if (ribbonGroup != null && ribbonGroup.Ribbon != null)
                manager = ribbonGroup.Ribbon.Manager;

            var ribbonSubItem = container as BarSubItem;
            if (ribbonSubItem != null)
                throw new System.Exception("Need to determine how to collect the Manager from a BarSubItem.  (Broken in DevExpress 14.2.5)");
                //manager = ribbonSubItem.Manager;

            if (manager == null)
                return null;

            // Find the template and return it
            var templateKey = new DataTemplateKey(item.GetType());
            return (DataTemplate)manager.TryFindResource(templateKey);
        }

        #endregion
    }
}
