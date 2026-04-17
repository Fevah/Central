using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

/// <summary>
/// Base class for all persistent entities.
/// Provides INotifyPropertyChanged, audit timestamps, and soft delete.
/// </summary>
public abstract class EntityBase : INotifyPropertyChanged
{
    private int _id;
    private bool _isDeleted;
    private DateTime? _deletedAt;
    private DateTime _createdAt;
    private DateTime _updatedAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }
    public bool IsDeleted { get => _isDeleted; set { _isDeleted = value; N(); } }
    public DateTime? DeletedAt { get => _deletedAt; set { _deletedAt = value; N(); } }

    /// <summary>Take a property snapshot for audit diffing.</summary>
    public Dictionary<string, object?> TakeSnapshot()
    {
        return GetType().GetProperties()
            .Where(p => p.CanRead && p.Name != nameof(TakeSnapshot))
            .ToDictionary(p => p.Name, p => p.GetValue(this));
    }

    // ── INotifyPropertyChanged ──

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void N([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        N(name);
        return true;
    }
}
