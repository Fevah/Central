using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class RoleSiteAccess : INotifyPropertyChanged
{
    private string _building = "";
    private bool _allowed = true;

    public string Building { get => _building; set { _building = value; OnPropertyChanged(); } }
    public bool   Allowed  { get => _allowed;  set { _allowed = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
