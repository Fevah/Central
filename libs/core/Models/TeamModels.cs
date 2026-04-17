using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

/// <summary>Department in the organizational hierarchy.</summary>
public class DepartmentRecord : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private int? _parentId;
    private string _parentName = "";
    private int? _headUserId;
    private string _headUserName = "";
    private string _costCenter = "";
    private string _description = "";
    private bool _isActive = true;
    private int _memberCount;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public int? ParentId { get => _parentId; set { _parentId = value; N(); } }
    public string ParentName { get => _parentName; set { _parentName = value; N(); } }
    public int? HeadUserId { get => _headUserId; set { _headUserId = value; N(); } }
    public string HeadUserName { get => _headUserName; set { _headUserName = value; N(); } }
    public string CostCenter { get => _costCenter; set { _costCenter = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public int MemberCount { get => _memberCount; set { _memberCount = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Team within a department.</summary>
public class TeamRecord : INotifyPropertyChanged
{
    private int _id;
    private int? _departmentId;
    private string _departmentName = "";
    private string _name = "";
    private string _description = "";
    private int? _teamLeadId;
    private string _teamLeadName = "";
    private bool _isActive = true;
    private int _memberCount;

    public int Id { get => _id; set { _id = value; N(); } }
    public int? DepartmentId { get => _departmentId; set { _departmentId = value; N(); } }
    public string DepartmentName { get => _departmentName; set { _departmentName = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public int? TeamLeadId { get => _teamLeadId; set { _teamLeadId = value; N(); } }
    public string TeamLeadName { get => _teamLeadName; set { _teamLeadName = value; N(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public int MemberCount { get => _memberCount; set { _memberCount = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Team membership record.</summary>
public class TeamMemberRecord
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string RoleInTeam { get; set; } = "member";
    public DateTime JoinedAt { get; set; }
}

/// <summary>Unified address record (polymorphic — company, contact, tenant, location).</summary>
public class AddressRecord : INotifyPropertyChanged
{
    private int _id;
    private string _entityType = "";
    private int _entityId;
    private string _addressType = "hq";
    private string _label = "";
    private string _line1 = "";
    private string _line2 = "";
    private string _line3 = "";
    private string _city = "";
    private string _stateRegion = "";
    private string _postalCode = "";
    private string _countryCode = "GB";
    private decimal? _latitude;
    private decimal? _longitude;
    private bool _isPrimary;
    private bool _isVerified;

    public int Id { get => _id; set { _id = value; N(); } }
    public string EntityType { get => _entityType; set { _entityType = value; N(); } }
    public int EntityId { get => _entityId; set { _entityId = value; N(); } }
    public string AddressType { get => _addressType; set { _addressType = value; N(); } }
    public string Label { get => _label; set { _label = value; N(); } }
    public string Line1 { get => _line1; set { _line1 = value; N(); } }
    public string Line2 { get => _line2; set { _line2 = value; N(); } }
    public string Line3 { get => _line3; set { _line3 = value; N(); } }
    public string City { get => _city; set { _city = value; N(); } }
    public string StateRegion { get => _stateRegion; set { _stateRegion = value; N(); } }
    public string PostalCode { get => _postalCode; set { _postalCode = value; N(); } }
    public string CountryCode { get => _countryCode; set { _countryCode = value; N(); } }
    public decimal? Latitude { get => _latitude; set { _latitude = value; N(); } }
    public decimal? Longitude { get => _longitude; set { _longitude = value; N(); } }
    public bool IsPrimary { get => _isPrimary; set { _isPrimary = value; N(); } }
    public bool IsVerified { get => _isVerified; set { _isVerified = value; N(); } }

    public string OneLine => string.Join(", ", new[] { Line1, City, PostalCode, CountryCode }.Where(s => !string.IsNullOrEmpty(s)));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>User profile (preferences, manager, skills).</summary>
public class UserProfile : INotifyPropertyChanged
{
    private int _id;
    private int _userId;
    private string _avatarUrl = "";
    private string _bio = "";
    private string _timezone = "UTC";
    private string _locale = "en-GB";
    private string _dateFormat = "dd/MM/yyyy";
    private string _timeFormat = "HH:mm";
    private string _linkedinUrl = "";
    private string _githubUrl = "";
    private string _phoneExt = "";
    private string _officeLocation = "";
    private DateTime? _startDate;
    private int? _managerId;
    private string _managerName = "";
    private string _skills = "";
    private string _certifications = "";

    public int Id { get => _id; set { _id = value; N(); } }
    public int UserId { get => _userId; set { _userId = value; N(); } }
    public string AvatarUrl { get => _avatarUrl; set { _avatarUrl = value; N(); } }
    public string Bio { get => _bio; set { _bio = value; N(); } }
    public string Timezone { get => _timezone; set { _timezone = value; N(); } }
    public string Locale { get => _locale; set { _locale = value; N(); } }
    public string DateFormat { get => _dateFormat; set { _dateFormat = value; N(); } }
    public string TimeFormat { get => _timeFormat; set { _timeFormat = value; N(); } }
    public string LinkedinUrl { get => _linkedinUrl; set { _linkedinUrl = value; N(); } }
    public string GithubUrl { get => _githubUrl; set { _githubUrl = value; N(); } }
    public string PhoneExt { get => _phoneExt; set { _phoneExt = value; N(); } }
    public string OfficeLocation { get => _officeLocation; set { _officeLocation = value; N(); } }
    public DateTime? StartDate { get => _startDate; set { _startDate = value; N(); } }
    public int? ManagerId { get => _managerId; set { _managerId = value; N(); } }
    public string ManagerName { get => _managerName; set { _managerName = value; N(); } }
    public string Skills { get => _skills; set { _skills = value; N(); } }
    public string Certifications { get => _certifications; set { _certifications = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>User invitation record.</summary>
public class InvitationRecord
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Viewer";
    public int? InvitedBy { get; set; }
    public string InvitedByName { get; set; } = "";
    public string Token { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsExpired => AcceptedAt == null && ExpiresAt < DateTime.UtcNow;
    public bool IsAccepted => AcceptedAt.HasValue;
    public string Status => IsAccepted ? "Accepted" : IsExpired ? "Expired" : "Pending";
}

/// <summary>Role template for quick role creation.</summary>
public class RoleTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] PermissionCodes { get; set; } = [];
    public bool IsSystem { get; set; }
}

/// <summary>Billing account for a tenant.</summary>
public class BillingAccount
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string StripeCustomerId { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public string BillingEmail { get; set; } = "";
    public string BillingName { get; set; } = "";
    public string Currency { get; set; } = "GBP";
    public bool TaxExempt { get; set; }
}

/// <summary>Invoice record.</summary>
public class InvoiceRecord
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public string Status { get; set; } = "draft";
    public string PdfUrl { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Tenant usage metric datapoint.</summary>
public class UsageMetric
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string MetricType { get; set; } = "";
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
}
