using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class TenantAddressRecord : INotifyPropertyChanged
{
    private int _id;
    private Guid _tenantId;
    private string _addressType = "billing";
    private string? _label;
    private string _line1 = "";
    private string? _line2;
    private string _city = "";
    private string? _state;
    private string? _postalCode;
    private string _country = "";
    private bool _isPrimary;

    public int Id { get => _id; set { _id = value; N(); } }
    public Guid TenantId { get => _tenantId; set { _tenantId = value; N(); } }
    public string AddressType { get => _addressType; set { _addressType = value; N(); } }
    public string? Label { get => _label; set { _label = value; N(); } }
    public string Line1 { get => _line1; set { _line1 = value; N(); } }
    public string? Line2 { get => _line2; set { _line2 = value; N(); } }
    public string City { get => _city; set { _city = value; N(); } }
    public string? State { get => _state; set { _state = value; N(); } }
    public string? PostalCode { get => _postalCode; set { _postalCode = value; N(); } }
    public string Country { get => _country; set { _country = value; N(); } }
    public bool IsPrimary { get => _isPrimary; set { _isPrimary = value; N(); } }

    // Display helpers
    public string FullAddress => string.Join(", ", new[] { Line1, Line2, City, State, PostalCode, Country }
        .Where(s => !string.IsNullOrWhiteSpace(s)));
    public string DisplayLabel => !string.IsNullOrEmpty(Label) ? Label : $"{AddressType} address";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
