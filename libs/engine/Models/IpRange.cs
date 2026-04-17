using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class IpRange : INotifyPropertyChanged
{
    private int _id;
    private string _region = "", _poolName = "", _block = "", _purpose = "", _notes = "", _status = "Active";

    public int Id { get => _id; set { _id = value; N(); } }
    public string Region { get => _region; set { _region = value; N(); } }
    public string PoolName { get => _poolName; set { _poolName = value; N(); } }
    public string Block { get => _block; set { _block = value; N(); } }
    public string Purpose { get => _purpose; set { _purpose = value; N(); } }
    public string Notes { get => _notes; set { _notes = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
