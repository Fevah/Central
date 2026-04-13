using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.Ribbon;

namespace TIG.TotalLink.Client.Module.Admin.TemplateSelector
{
    /// <summary>
    /// Template selector for classes that inherit RibbonCategoryViewModelBase.
    /// </summary>
    public class RibbonCategoryTemplateSelector : DataTemplateSelector
    {
        #region Overrides

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null)
                return null;

            // Find the RibbonControl that this item belongs to
            var ribbonControl = container as RibbonControl;
            if (ribbonControl == null)
                return null;

            // Find the template and return it
            var templateKey = new DataTemplateKey(item.GetType());
            return (DataTemplate)ribbonControl.TryFindResource(templateKey);
        }

        #endregion
    }
}
