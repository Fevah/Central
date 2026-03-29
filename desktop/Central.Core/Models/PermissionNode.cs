using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

/// <summary>
/// Tree node for the permissions TreeList. Parents are modules, children are permissions.
/// </summary>
public class PermissionNode : INotifyPropertyChanged
{
    private string _key = "";
    private string _parentKey = "";
    private string _displayName = "";
    private bool _isEnabled;
    private string _module = "";     // e.g. "devices", "switches", "admin"
    private string _permission = ""; // e.g. "View", "Edit", "Delete", "ViewReserved", or "" for parent

    public string Key { get => _key; set { _key = value; N(); } }
    public string ParentKey { get => _parentKey; set { _parentKey = value; N(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; N(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; N(); } }
    public string Module { get => _module; set { _module = value; N(); } }
    public string Permission { get => _permission; set { _permission = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
