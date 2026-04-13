using System.Windows;
using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.TemplateSelector
{
    public class WidgetCustomizerTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return (DataTemplate)((FrameworkElement)container).TryFindResource(GetTemplateName(item as WidgetCustomizerViewModelBase));
        }

        private static string GetTemplateName(WidgetCustomizerViewModelBase customizer)
        {
            if (customizer == null)
            {
                return "DefaultWidgetCustomizerTemplate";
            }

            return string.Format("{0}Template", customizer.GetType().Name.Replace("ViewModel", ""));
        }
    }
}
