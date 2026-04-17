using System.ComponentModel;

namespace Central.Engine.Models;

public class CompanyRecord : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private string _legalName = "";
    private string _registrationNo = "";
    private string _taxId = "";
    private string _industry = "";
    private string _sizeBand = "";
    private string _website = "";
    private string _logoUrl = "";
    private int? _parentId;
    private string _parentName = "";
    private bool _isActive = true;
    private int? _createdBy;
    private DateTime? _createdAt;
    private DateTime? _updatedAt;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
    public string LegalName { get => _legalName; set { _legalName = value; OnPropertyChanged(nameof(LegalName)); } }
    public string RegistrationNo { get => _registrationNo; set { _registrationNo = value; OnPropertyChanged(nameof(RegistrationNo)); } }
    public string TaxId { get => _taxId; set { _taxId = value; OnPropertyChanged(nameof(TaxId)); } }
    public string Industry { get => _industry; set { _industry = value; OnPropertyChanged(nameof(Industry)); } }
    public string SizeBand { get => _sizeBand; set { _sizeBand = value; OnPropertyChanged(nameof(SizeBand)); } }
    public string Website { get => _website; set { _website = value; OnPropertyChanged(nameof(Website)); } }
    public string LogoUrl { get => _logoUrl; set { _logoUrl = value; OnPropertyChanged(nameof(LogoUrl)); } }
    public int? ParentId { get => _parentId; set { _parentId = value; OnPropertyChanged(nameof(ParentId)); } }
    public string ParentName { get => _parentName; set { _parentName = value; OnPropertyChanged(nameof(ParentName)); } }
    public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(nameof(IsActive)); } }
    public int? CreatedBy { get => _createdBy; set { _createdBy = value; OnPropertyChanged(nameof(CreatedBy)); } }
    public DateTime? CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); } }
    public DateTime? UpdatedAt { get => _updatedAt; set { _updatedAt = value; OnPropertyChanged(nameof(UpdatedAt)); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
