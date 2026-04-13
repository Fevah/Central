using System.Windows;
using System.Windows.Controls;

namespace TIG.TotalLink.Client.Module.Admin.TemplateSelector
{
    /// <summary>
    /// Template selector for classes that inherit BackstageItemViewModelBase.
    /// </summary>
    public class BackstageItemTemplateSelector : DataTemplateSelector
    {
        #region Overrides

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null)
                return null;

            var contentPresenter = container as ContentPresenter;
            if (contentPresenter == null)
                return null;

            // Find the template and return it
            var templateKey = new DataTemplateKey(item.GetType());
            return (DataTemplate)contentPresenter.TryFindResource(templateKey);
        }

        #endregion
    }
}
