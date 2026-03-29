using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

/// <summary>Flat tree node for ribbon customizer. TreeListControl uses Id/ParentId for hierarchy.</summary>
public class RibbonTreeItem : INotifyPropertyChanged
{
    private int _id;
    private int _parentId;
    private string _nodeType = "";   // "page", "group", "item", "separator"
    private string _text = "";
    private string? _iconName;
    private string? _customText;
    private bool _isHidden;
    private int _sortOrder;
    private string? _permission;
    private string _itemKey = "";    // "Page/Group/Item" path for override matching

    public int Id { get => _id; set { _id = value; N(); } }
    public int ParentId { get => _parentId; set { _parentId = value; N(); } }
    public string NodeType { get => _nodeType; set { _nodeType = value; N(); N(nameof(NodeIcon)); } }
    public string Text { get => _text; set { _text = value; N(); } }
    public string? IconName { get => _iconName; set { _iconName = value; N(); N(nameof(IconPreview)); } }

    /// <summary>Rendered icon preview. Set by the shell after icon selection.</summary>
    private object? _iconPreview;
    public object? IconPreview { get => _iconPreview; set { _iconPreview = value; N(); } }
    public string? CustomText { get => _customText; set { _customText = value; N(); N(nameof(DisplayText)); } }
    public bool IsHidden { get => _isHidden; set { _isHidden = value; N(); N(nameof(HiddenIcon)); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }
    public string? Permission { get => _permission; set { _permission = value; N(); } }
    public string ItemKey { get => _itemKey; set { _itemKey = value; N(); } }

    // Admin-specific fields
    private string? _defaultIcon;
    private string? _defaultLabel;
    private string? _itemType;
    public string? DefaultIcon { get => _defaultIcon; set { _defaultIcon = value; N(); } }
    public string? DefaultLabel { get => _defaultLabel; set { _defaultLabel = value; N(); } }
    public string? ItemType { get => _itemType; set { _itemType = value; N(); } }

    // Display style + linking
    private string _displayStyle = "small";
    private string? _linkTarget;
    /// <summary>Display style: 'large' (icon+label below), 'small' (icon+label right), 'smallNoText' (icon only)</summary>
    public string DisplayStyle { get => _displayStyle; set { _displayStyle = value; N(); } }
    /// <summary>Link target: 'panel:PanelName', 'url:https://...', 'action:ActionKey', 'page:PageName'</summary>
    public string? LinkTarget { get => _linkTarget; set { _linkTarget = value; N(); } }

    /// <summary>What to display — custom text if set, otherwise default text.</summary>
    public string DisplayText => !string.IsNullOrEmpty(CustomText) ? CustomText : Text;

    /// <summary>Node type icon for tree display.</summary>
    public string NodeIcon => NodeType switch
    {
        "page" => "📑",
        "group" => "📁",
        "item" => "🔘",
        "separator" => "───",
        _ => "•"
    };

    /// <summary>Hidden indicator.</summary>
    public string HiddenIcon => IsHidden ? "👁‍🗨" : "";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
