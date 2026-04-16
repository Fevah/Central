using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class ModuleLicenseRecord : INotifyPropertyChanged
{
    private int _id;
    private Guid _tenantId;
    private string _tenantSlug = "";
    private string _tenantName = "";
    private int _moduleId;
    private string _moduleCode = "";
    private string _moduleName = "";
    private bool _isBase;
    private DateTime _grantedAt;
    private DateTime? _expiresAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public Guid TenantId { get => _tenantId; set { _tenantId = value; N(); } }
    public string TenantSlug { get => _tenantSlug; set { _tenantSlug = value; N(); } }
    public string TenantName { get => _tenantName; set { _tenantName = value; N(); } }
    public int ModuleId { get => _moduleId; set { _moduleId = value; N(); } }
    public string ModuleCode { get => _moduleCode; set { _moduleCode = value; N(); } }
    public string ModuleName { get => _moduleName; set { _moduleName = value; N(); } }
    public bool IsBase { get => _isBase; set { _isBase = value; N(); N(nameof(LicenseType)); } }
    public DateTime GrantedAt { get => _grantedAt; set { _grantedAt = value; N(); } }
    public DateTime? ExpiresAt { get => _expiresAt; set { _expiresAt = value; N(); N(nameof(IsExpired)); } }

    // UI helpers
    public string LicenseType => IsBase ? "Base" : "Licensed";
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
