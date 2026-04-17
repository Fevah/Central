namespace Central.Engine.Modules;

/// <summary>
/// Module that contributes ribbon tabs/groups/buttons.
/// </summary>
public interface IModuleRibbon
{
    void RegisterRibbon(IRibbonBuilder ribbon);
}

/// <summary>Fluent API for building ribbon from modules.</summary>
public interface IRibbonBuilder
{
    IRibbonPageBuilder AddPage(string header, int sortOrder, Action<IRibbonPageBuilder> configure);
    IRibbonPageBuilder AddPage(string header, int sortOrder, string? requiredPermission, Action<IRibbonPageBuilder> configure);
}

public interface IRibbonPageBuilder
{
    IRibbonGroupBuilder AddGroup(string header, Action<IRibbonGroupBuilder> configure);
}

public interface IRibbonGroupBuilder
{
    void AddButton(string content, string? permission, string? glyph, Action onClick);
    void AddLargeButton(string content, string? permission, string? largeGlyph, string? toolTip, Action onClick);
    void AddCheckButton(string content, string panelId);
    void AddToggleButton(string content, string? permission, Action<bool> onToggle);
    void AddSeparator();
    void AddSplitButton(string content, string? permission, string? glyph, Action? primaryClick, Action<List<Central.Engine.Shell.RibbonButtonRegistration>> configureSubItems);
}
