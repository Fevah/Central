using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class ApiKeyRecord : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private string _role = "Viewer";
    private bool _isActive = true;
    private DateTime? _createdAt;
    private DateTime? _lastUsedAt;
    private int _useCount;
    private DateTime? _expiresAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string Role { get => _role; set { _role = value; N(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public DateTime? CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime? LastUsedAt { get => _lastUsedAt; set { _lastUsedAt = value; N(); } }
    public int UseCount { get => _useCount; set { _useCount = value; N(); } }
    public DateTime? ExpiresAt { get => _expiresAt; set { _expiresAt = value; N(); } }

    /// <summary>Only populated on create — the raw key shown once to the admin.</summary>
    public string? RawKey { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
