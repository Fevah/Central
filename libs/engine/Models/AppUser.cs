using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class AppUser : INotifyPropertyChanged
{
    private int    _id;
    private string _username    = "";
    private string _displayName = "";
    private string _role        = "Viewer";
    private bool   _isActive    = true;
    private bool   _autoLogin;
    private string _userType    = "ActiveDirectory";  // ActiveDirectory, Manual, System
    private string _email       = "";
    private string _adSid       = "";
    private string _passwordHash = "";
    private string _salt        = "";
    private string _department  = "";
    private string _title       = "";
    private string _phone       = "";
    private string _mobile      = "";
    private string _company     = "";
    private string _adGuid      = "";
    private DateTime? _lastAdSync;
    private DateTime? _lastLoginAt;
    private int    _loginCount;
    private DateTime? _createdAt;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); } }
    public string Role { get => _role; set { _role = value; OnPropertyChanged(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }
    public bool AutoLogin { get => _autoLogin; set { _autoLogin = value; OnPropertyChanged(); } }
    public string UserType { get => _userType; set { _userType = value; OnPropertyChanged(); } }
    public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
    public string AdSid { get => _adSid; set { _adSid = value; OnPropertyChanged(); } }
    public string PasswordHash { get => _passwordHash; set { _passwordHash = value; OnPropertyChanged(); } }
    public string Salt { get => _salt; set { _salt = value; OnPropertyChanged(); } }
    public string Department { get => _department; set { _department = value; OnPropertyChanged(); } }
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
    public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }
    public string Mobile { get => _mobile; set { _mobile = value; OnPropertyChanged(); } }
    public string Company { get => _company; set { _company = value; OnPropertyChanged(); } }
    public string AdGuid { get => _adGuid; set { _adGuid = value; OnPropertyChanged(); } }
    public DateTime? LastAdSync { get => _lastAdSync; set { _lastAdSync = value; OnPropertyChanged(); } }
    public DateTime? LastLoginAt { get => _lastLoginAt; set { _lastLoginAt = value; OnPropertyChanged(); } }
    public int LoginCount { get => _loginCount; set { _loginCount = value; OnPropertyChanged(); } }
    public DateTime? CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }

    /// <summary>True if this user was authenticated via Active Directory (not manual login).</summary>
    public bool IsAdUser => UserType == "ActiveDirectory";

    /// <summary>True if this is a system-created user that can't be deleted.</summary>
    public bool IsSystemUser => UserType == "System";

    /// <summary>True if this user type is protected from deletion (System, Service).</summary>
    public bool IsProtected => Auth.UserTypes.IsProtected(UserType);

    /// <summary>Display initials for avatar (first letter of first+last name).</summary>
    public string Initials
    {
        get
        {
            var name = !string.IsNullOrEmpty(DisplayName) ? DisplayName : Username;
            var parts = name.Split(' ', '.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                : name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
        }
    }

    /// <summary>Status display text for the grid.</summary>
    public string StatusText => IsActive ? "Active" : "Inactive";

    /// <summary>Status color for grid row highlighting.</summary>
    public string StatusColor => IsActive ? "#22C55E" : "#6B7280";

    /// <summary>Detail: permissions granted via this user's role. Populated on expand.</summary>
    public System.Collections.ObjectModel.ObservableCollection<UserPermissionDetail> DetailPermissions { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Permission detail for user master-detail expansion.</summary>
public class UserPermissionDetail
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public bool Granted { get; set; }
}
