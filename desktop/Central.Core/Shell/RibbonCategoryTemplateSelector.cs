using System.Windows;
using System.Windows.Controls;

namespace Central.Core.Shell;

/// <summary>
/// Routes ribbon category/page/group ViewModel types to their XAML DataTemplates.
/// Based on TotalLink's RibbonCategoryTemplateSelector.
///
/// Usage: assign as RibbonControl.CategoryTemplateSelector in the shell.
/// Each ViewModel type (RibbonPageRegistration, RibbonGroupRegistration, etc.)
/// must have a corresponding DataTemplate defined as a resource.
///
/// TotalLink source: RibbonCategoryTemplateSelector.cs (31 lines)
/// Pattern: DataTemplateKey(item.GetType()) → TryFindResource
/// </summary>
public class RibbonCategoryTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item == null || container is not FrameworkElement fe) return null;
        var key = new DataTemplateKey(item.GetType());
        return fe.TryFindResource(key) as DataTemplate;
    }
}
