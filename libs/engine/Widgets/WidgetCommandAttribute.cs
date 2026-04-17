namespace Central.Engine.Widgets;

/// <summary>
/// Marks an ICommand property on a WidgetViewModelBase to auto-generate
/// a ribbon button when the widget's panel has focus.
/// Based on TotalLink's WidgetCommandAttribute.
///
/// Text replacement: {Type} and {TypePlural} are replaced at runtime
/// with the entity type name (e.g. "P2P Link", "P2P Links").
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class WidgetCommandAttribute : Attribute
{
    public string Name { get; }
    public string GroupName { get; }
    public string Description { get; }
    public object? CommandParameter { get; set; }

    /// <param name="name">Button text — supports {Type}, {TypePlural} placeholders</param>
    /// <param name="groupName">Ribbon group name — e.g. "Edit", "Data", "View"</param>
    /// <param name="description">Tooltip text — supports same placeholders</param>
    public WidgetCommandAttribute(string name, string groupName, string description = "")
    {
        Name = name;
        GroupName = groupName;
        Description = description;
    }
}
