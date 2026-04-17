using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class AppLogEntry : INotifyPropertyChanged
{
    private int _id;
    private DateTime _timestamp = DateTime.UtcNow;
    private string _level = "Error";
    private string _tag = "";
    private string _source = "";
    private string _message = "";
    private string _detail = "";
    private string _username = "";

    public int Id { get => _id; set { _id = value; N(); } }
    public DateTime Timestamp { get => _timestamp; set { _timestamp = value; N(); } }
    public string Level { get => _level; set { _level = value; N(); } }
    public string Tag { get => _tag; set { _tag = value; N(); } }
    public string Source { get => _source; set { _source = value; N(); } }
    public string Message { get => _message; set { _message = value; N(); } }
    public string Detail { get => _detail; set { _detail = value; N(); } }
    public string Username { get => _username; set { _username = value; N(); } }

    public string DisplayTime => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
