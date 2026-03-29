using Central.Core.Auth;
using Central.Core.Modules;

namespace Central.Core.Shell;

/// <summary>
/// Collects ribbon page registrations from modules.
/// The Shell reads this to build the DevExpress RibbonControl.
///
/// Based on TotalLink's DocumentManagerView.xaml.cs pattern —
/// ribbon categories bound via CategoriesSourceProperty.
/// </summary>
public class RibbonPageRegistration
{
    public string Header { get; set; } = "";
    public int SortOrder { get; set; }
    /// <summary>If set, the entire page is hidden unless the user has this permission.</summary>
    public string? RequiredPermission { get; set; }
    public List<RibbonGroupRegistration> Groups { get; } = new();
}

public class RibbonGroupRegistration
{
    public string Header { get; set; } = "";
    public List<IRibbonItemRegistration> Items { get; } = new();
    // Convenience accessors for backward compat
    public List<RibbonButtonRegistration> Buttons => Items.OfType<RibbonButtonRegistration>().ToList();
    public List<RibbonCheckButtonRegistration> CheckButtons => Items.OfType<RibbonCheckButtonRegistration>().ToList();
    public List<RibbonToggleRegistration> ToggleButtons => Items.OfType<RibbonToggleRegistration>().ToList();
    public List<RibbonSplitButtonRegistration> SplitButtons => Items.OfType<RibbonSplitButtonRegistration>().ToList();
}

/// <summary>Marker interface for all ribbon item types.</summary>
public interface IRibbonItemRegistration { }

public class RibbonButtonRegistration : IRibbonItemRegistration
{
    public string Content { get; set; } = "";
    public string? Permission { get; set; }
    public string? Glyph { get; set; }
    public string? LargeGlyph { get; set; }
    public bool IsLarge { get; set; }
    public string? ToolTip { get; set; }
    public Action OnClick { get; set; } = () => { };
}

public class RibbonCheckButtonRegistration : IRibbonItemRegistration
{
    public string Content { get; set; } = "";
    public string PanelId { get; set; } = "";
    public string? Glyph { get; set; }
}

public class RibbonToggleRegistration : IRibbonItemRegistration
{
    public string Content { get; set; } = "";
    public string? Permission { get; set; }
    public string? Glyph { get; set; }
    public Action<bool> OnToggle { get; set; } = _ => { };
}

public class RibbonSeparatorRegistration : IRibbonItemRegistration { }

public class RibbonSplitButtonRegistration : IRibbonItemRegistration
{
    public string Content { get; set; } = "";
    public string? Permission { get; set; }
    public string? Glyph { get; set; }
    public string? LargeGlyph { get; set; }
    public bool IsLarge { get; set; }
    /// <summary>Primary action when clicking the button (not the dropdown arrow).</summary>
    public Action? OnClick { get; set; }
    /// <summary>Sub-items shown in the dropdown.</summary>
    public List<RibbonButtonRegistration> SubItems { get; } = new();
}

/// <summary>
/// Fluent IRibbonBuilder implementation that collects module contributions.
/// </summary>
public class RibbonBuilder : IRibbonBuilder
{
    public List<RibbonPageRegistration> Pages { get; } = new();

    public IRibbonPageBuilder AddPage(string header, int sortOrder, Action<IRibbonPageBuilder> configure)
        => AddPage(header, sortOrder, null, configure);

    public IRibbonPageBuilder AddPage(string header, int sortOrder, string? requiredPermission, Action<IRibbonPageBuilder> configure)
    {
        var page = new RibbonPageRegistration { Header = header, SortOrder = sortOrder, RequiredPermission = requiredPermission };
        var builder = new RibbonPageBuilder(page);
        configure(builder);
        Pages.Add(page);
        return builder;
    }

    /// <summary>Filter pages by current user's permissions. Hides pages with no visible items.</summary>
    public List<RibbonPageRegistration> GetVisiblePages()
    {
        var auth = AuthContext.Instance;
        return Pages
            .Where(p =>
            {
                // Page-level permission gate
                if (!string.IsNullOrEmpty(p.RequiredPermission) && !auth.HasPermission(p.RequiredPermission))
                    return false;
                // At least one visible item in any group
                return p.Groups.Any(g => g.Items.Any(item => item switch
                {
                    RibbonButtonRegistration btn => string.IsNullOrEmpty(btn.Permission) || auth.HasPermission(btn.Permission),
                    RibbonSplitButtonRegistration split => string.IsNullOrEmpty(split.Permission) || auth.HasPermission(split.Permission),
                    RibbonToggleRegistration toggle => string.IsNullOrEmpty(toggle.Permission) || auth.HasPermission(toggle.Permission),
                    RibbonCheckButtonRegistration => true,
                    RibbonSeparatorRegistration => false, // separators alone don't justify showing a page
                    _ => false
                }));
            })
            .OrderBy(p => p.SortOrder)
            .ToList();
    }
}

internal class RibbonPageBuilder : IRibbonPageBuilder
{
    private readonly RibbonPageRegistration _page;
    public RibbonPageBuilder(RibbonPageRegistration page) => _page = page;

    public IRibbonGroupBuilder AddGroup(string header, Action<IRibbonGroupBuilder> configure)
    {
        var group = new RibbonGroupRegistration { Header = header };
        var builder = new RibbonGroupBuilder(group);
        configure(builder);
        _page.Groups.Add(group);
        return builder;
    }
}

internal class RibbonGroupBuilder : IRibbonGroupBuilder
{
    private readonly RibbonGroupRegistration _group;
    public RibbonGroupBuilder(RibbonGroupRegistration group) => _group = group;

    public void AddButton(string content, string? permission, string? glyph, Action onClick)
    {
        _group.Items.Add(new RibbonButtonRegistration
        {
            Content = content, Permission = permission, Glyph = glyph, OnClick = onClick
        });
    }

    public void AddLargeButton(string content, string? permission, string? largeGlyph, string? toolTip, Action onClick)
    {
        _group.Items.Add(new RibbonButtonRegistration
        {
            Content = content, Permission = permission, LargeGlyph = largeGlyph, IsLarge = true, ToolTip = toolTip, OnClick = onClick
        });
    }

    public void AddCheckButton(string content, string panelId)
    {
        _group.Items.Add(new RibbonCheckButtonRegistration
        {
            Content = content, PanelId = panelId
        });
    }

    public void AddToggleButton(string content, string? permission, Action<bool> onToggle)
    {
        _group.Items.Add(new RibbonToggleRegistration
        {
            Content = content, Permission = permission, OnToggle = onToggle
        });
    }

    public void AddSeparator()
    {
        _group.Items.Add(new RibbonSeparatorRegistration());
    }

    public void AddSplitButton(string content, string? permission, string? glyph, Action? primaryClick, Action<List<RibbonButtonRegistration>> configureSubItems)
    {
        var split = new RibbonSplitButtonRegistration
        {
            Content = content, Permission = permission, Glyph = glyph, OnClick = primaryClick
        };
        configureSubItems(split.SubItems);
        _group.Items.Add(split);
    }
}
