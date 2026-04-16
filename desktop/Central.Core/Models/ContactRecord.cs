using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class ContactRecord : INotifyPropertyChanged
{
    private int _id;
    private string _firstName = "";
    private string _lastName = "";
    private string? _email;
    private string? _phone;
    private string? _mobile;
    private string? _jobTitle;
    private string? _company;
    private string? _notes;

    public int Id { get => _id; set { _id = value; N(); } }
    public string FirstName { get => _firstName; set { _firstName = value; N(); N(nameof(FullName)); } }
    public string LastName { get => _lastName; set { _lastName = value; N(); N(nameof(FullName)); } }
    public string? Email { get => _email; set { _email = value; N(); } }
    public string? Phone { get => _phone; set { _phone = value; N(); } }
    public string? Mobile { get => _mobile; set { _mobile = value; N(); } }
    public string? JobTitle { get => _jobTitle; set { _jobTitle = value; N(); } }
    public string? Company { get => _company; set { _company = value; N(); } }
    public string? Notes { get => _notes; set { _notes = value; N(); } }

    // Display helpers
    public string FullName => $"{FirstName} {LastName}".Trim();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Contact-tenant junction for the many-to-many relationship.</summary>
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

    // Display from JOINs
    public string TenantSlug { get => _tenantSlug; set { _tenantSlug = value; N(); } }
    public string ContactName { get => _contactName; set { _contactName = value; N(); } }
    public string? ContactEmail { get => _contactEmail; set { _contactEmail = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
