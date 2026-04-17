using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

/// <summary>
/// Full CRM-ready contact model — unified across SD, CRM, and platform.
/// Phase 2 of the Enterprise CRM Buildout.
/// </summary>
public class ContactRecord : INotifyPropertyChanged
{
    private int _id;
    private int? _companyId;
    private string _companyName = "";
    private string _prefix = "";
    private string _firstName = "";
    private string _lastName = "";
    private string? _email;
    private string? _phone;
    private string? _mobile;
    private string? _jobTitle;
    private string? _department;
    private string? _linkedinUrl;
    private bool _isPrimary;
    private string _contactType = "customer";
    private string _status = "active";
    private string? _source;
    private string _tags = "";
    private string? _notes;
    private string? _avatarUrl;
    private string? _company;  // Legacy text field (backward compat)
    private DateTime? _createdAt;
    private DateTime? _updatedAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int? CompanyId { get => _companyId; set { _companyId = value; N(); } }
    public string CompanyName { get => _companyName; set { _companyName = value; N(); } }
    public string Prefix { get => _prefix; set { _prefix = value; N(); } }
    public string FirstName { get => _firstName; set { _firstName = value; N(); N(nameof(FullName)); } }
    public string LastName { get => _lastName; set { _lastName = value; N(); N(nameof(FullName)); } }
    public string? Email { get => _email; set { _email = value; N(); } }
    public string? Phone { get => _phone; set { _phone = value; N(); } }
    public string? Mobile { get => _mobile; set { _mobile = value; N(); } }
    public string? JobTitle { get => _jobTitle; set { _jobTitle = value; N(); } }
    public string? Department { get => _department; set { _department = value; N(); } }
    public string? LinkedinUrl { get => _linkedinUrl; set { _linkedinUrl = value; N(); } }
    public bool IsPrimary { get => _isPrimary; set { _isPrimary = value; N(); } }
    public string ContactType { get => _contactType; set { _contactType = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }
    public string? Source { get => _source; set { _source = value; N(); } }
    public string Tags { get => _tags; set { _tags = value; N(); } }
    public string? Notes { get => _notes; set { _notes = value; N(); } }
    public string? AvatarUrl { get => _avatarUrl; set { _avatarUrl = value; N(); } }
    public string? Company { get => _company; set { _company = value; N(); } }
    public DateTime? CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime? UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Contact communication log entry.</summary>
public class ContactCommunication
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public string Channel { get; set; } = "";
    public string Direction { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public int? LoggedBy { get; set; }
    public string LoggedByName { get; set; } = "";
}

/// <summary>Contact-tenant junction for the many-to-many relationship (platform-level).</summary>
public class TenantContactRecord : INotifyPropertyChanged
{
    private int _id;
    private Guid _tenantId;
    private int _contactId;
    private string _role = "primary";
    private bool _isPrimary;
    private string _tenantSlug = "";
    private string _contactName = "";
    private string? _contactEmail;

    public int Id { get => _id; set { _id = value; N(); } }
    public Guid TenantId { get => _tenantId; set { _tenantId = value; N(); } }
    public int ContactId { get => _contactId; set { _contactId = value; N(); } }
    public string Role { get => _role; set { _role = value; N(); } }
    public bool IsPrimary { get => _isPrimary; set { _isPrimary = value; N(); } }
    public string TenantSlug { get => _tenantSlug; set { _tenantSlug = value; N(); } }
    public string ContactName { get => _contactName; set { _contactName = value; N(); } }
    public string? ContactEmail { get => _contactEmail; set { _contactEmail = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
