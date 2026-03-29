using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class Country : INotifyPropertyChanged
{
    private int _id;
    private string _code = "";
    private string _name = "";
    private int _sortOrder;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Code { get => _code; set { _code = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Region : INotifyPropertyChanged
{
    private int _id;
    private int _countryId;
    private string _code = "";
    private string _name = "";
    private int _sortOrder;
    private string _countryName = "";

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public int CountryId { get => _countryId; set { _countryId = value; OnPropertyChanged(); } }
    public string Code { get => _code; set { _code = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); } }
    public string CountryName { get => _countryName; set { _countryName = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Postcode : INotifyPropertyChanged
{
    private int _id;
    private int _regionId;
    private string _code = "";
    private string _locality = "";
    private decimal? _latitude;
    private decimal? _longitude;
    private string _regionName = "";

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public int RegionId { get => _regionId; set { _regionId = value; OnPropertyChanged(); } }
    public string Code { get => _code; set { _code = value; OnPropertyChanged(); } }
    public string Locality { get => _locality; set { _locality = value; OnPropertyChanged(); } }
    public decimal? Latitude { get => _latitude; set { _latitude = value; OnPropertyChanged(); } }
    public decimal? Longitude { get => _longitude; set { _longitude = value; OnPropertyChanged(); } }
    public string RegionName { get => _regionName; set { _regionName = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
