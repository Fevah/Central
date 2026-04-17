using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class MstpConfig : INotifyPropertyChanged
{
    private int _id;
    private string _building = "", _deviceName = "", _deviceRole = "", _mstpPriority = "", _notes = "", _status = "Active";

    public int Id { get => _id; set { _id = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public string DeviceName { get => _deviceName; set { _deviceName = value; N(); } }
    public string DeviceRole { get => _deviceRole; set { _deviceRole = value; N(); } }
    public string MstpPriority { get => _mstpPriority; set { _mstpPriority = value; N(); } }
    public string Notes { get => _notes; set { _notes = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
