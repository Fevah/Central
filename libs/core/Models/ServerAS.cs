using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class ServerAS : INotifyPropertyChanged
{
    private int _id;
    private string _building = "", _serverAs = "", _status = "Active";

    public int Id { get => _id; set { _id = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public string ServerAsn { get => _serverAs; set { _serverAs = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
