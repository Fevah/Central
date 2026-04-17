using System.ComponentModel;

namespace Central.Core.Models;

/// <summary>DB-backed ribbon page configuration.</summary>
public class RibbonPageConfig : INotifyPropertyChanged
{
    private int _id;
    private string _header = "";
    private int _sortOrder;
    private string? _requiredPermission;
    private string? _iconName;
    private bool _isVisible = true;
    private bool _isSystem;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Header { get => _header; set { _header = value; OnPropertyChanged(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); } }
    public string? RequiredPermission { get => _requiredPermission; set { _requiredPermission = value; OnPropertyChanged(); } }
    public string? IconName { get => _iconName; set { _iconName = value; OnPropertyChanged(); } }
    public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChanged(); } }
    public bool IsSystem { get => _isSystem; set { _isSystem = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>DB-backed ribbon group configuration.</summary>
public class RibbonGroupConfig : INotifyPropertyChanged
{
    private int _id;
    private int _pageId;
    private string _header = "";
    private int _sortOrder;
    private bool _isVisible = true;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public int PageId { get => _pageId; set { _pageId = value; OnPropertyChanged(); } }
    public string Header { get => _header; set { _header = value; OnPropertyChanged(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); } }
    public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChanged(); } }

    // Navigation
    public string? PageHeader { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>DB-backed ribbon item configuration.</summary>
public class RibbonItemConfig : INotifyPropertyChanged
{
    private int _id;
    private int _groupId;
    private string _content = "";
    private string _itemType = "button";
    private int _sortOrder;
    private string? _permission;
    private string? _glyph;
    private string? _largeGlyph;
    private int? _iconId;
    private string? _commandType;
    private string? _commandParam;
    private string? _tooltip;
    private bool _isVisible = true;
    private bool _isSystem;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public int GroupId { get => _groupId; set { _groupId = value; OnPropertyChanged(); } }
    public string Content { get => _content; set { _content = value; OnPropertyChanged(); } }
    public string ItemType { get => _itemType; set { _itemType = value; OnPropertyChanged(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); } }
    public string? Permission { get => _permission; set { _permission = value; OnPropertyChanged(); } }
    public string? Glyph { get => _glyph; set { _glyph = value; OnPropertyChanged(); } }
    public string? LargeGlyph { get => _largeGlyph; set { _largeGlyph = value; OnPropertyChanged(); } }
    public int? IconId { get => _iconId; set { _iconId = value; OnPropertyChanged(); } }
    public string? CommandType { get => _commandType; set { _commandType = value; OnPropertyChanged(); } }
    public string? CommandParam { get => _commandParam; set { _commandParam = value; OnPropertyChanged(); } }
    public string? Tooltip { get => _tooltip; set { _tooltip = value; OnPropertyChanged(); } }
    public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChanged(); } }
    public bool IsSystem { get => _isSystem; set { _isSystem = value; OnPropertyChanged(); } }

    // Navigation
    public string? GroupHeader { get; set; }
    public string? PageHeader { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Per-user ribbon item override (icon, text, visibility).</summary>
public class UserRibbonOverride
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ItemKey { get; set; } = "";  // "Page/Group/Item" path
    public string? CustomIcon { get; set; }
    public string? CustomText { get; set; }
    public bool IsHidden { get; set; }
    public int? SortOrder { get; set; }
}
