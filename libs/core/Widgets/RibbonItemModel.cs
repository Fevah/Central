using System.Windows.Input;

namespace Central.Core.Widgets;

/// <summary>
/// Lightweight ribbon item model generated from [WidgetCommand] reflection.
/// Used by the shell to build DevExpress ribbon items at runtime.
/// </summary>
public class RibbonGroupModel
{
    public string Name { get; set; } = "";
    public List<RibbonItemModel> Items { get; } = new();
}

public class RibbonItemModel
{
    public string Content { get; set; } = "";
    public string Description { get; set; } = "";
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public string? Permission { get; set; }
}
