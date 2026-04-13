using System.Windows;
using System.Windows.Controls;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.TemplateSelector
{
    public class GridColumnTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return (DataTemplate)((FrameworkElement)container).TryFindResource(GetTemplateName(item as GridColumnWrapperBase));
        }

        private static string GetTemplateName(GridColumnWrapperBase column)
        {
            if (column == null || column.Editor == null)
            {
                return "DefaultGridColumnTemplate";
            }

            return string.Format("{0}GridColumnTemplate", column.Editor.GetType().Name.Replace("EditorDefinition", ""));
        }
    }
}
