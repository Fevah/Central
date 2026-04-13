using System.Windows;
using System.Windows.Controls;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;

namespace TIG.TotalLink.Client.Editor.TemplateSelector
{
    public class DataLayoutItemTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return (DataTemplate)((FrameworkElement)container).TryFindResource(GetTemplateName(item as DataLayoutItemWrapper));
        }

        private static string GetTemplateName(DataLayoutItemWrapper layoutItem)
        {
            if (layoutItem == null || layoutItem.Editor == null)
            {
                return "DefaultDataLayoutItemTemplate";
            }

            return string.Format("{0}DataLayoutItemTemplate", layoutItem.Editor.GetType().Name.Replace("EditorDefinition", ""));
        }
    }
}
