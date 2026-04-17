using System.ComponentModel;

namespace Central.Engine.Services;

/// <summary>
/// Lightweight undo/redo service. Captures property changes on INotifyPropertyChanged objects.
/// Based on TotalLink's MonitoredUndo pattern but without the NuGet dependency.
/// </summary>
public class UndoService
{
    public static UndoService Instance { get; } = new();

    private readonly Stack<UndoBatch> _undoStack = new();
    private readonly Stack<UndoBatch> _redoStack = new();
    private UndoBatch? _currentBatch;
    private bool _isUndoRedoing;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string? UndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
    public string? RedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;
    public IReadOnlyList<string> UndoHistory => _undoStack.Select(b => b.Description).ToList();
    public IReadOnlyList<string> RedoHistory => _redoStack.Select(b => b.Description).ToList();

    public event EventHandler? StateChanged;

    /// <summary>Begin a batch of related changes (e.g., editing a row).</summary>
    public void BeginBatch(string description)
    {
        if (_isUndoRedoing) return;
        _currentBatch = new UndoBatch(description);
    }

    /// <summary>Commit the current batch to the undo stack.</summary>
    public void CommitBatch()
    {
        if (_isUndoRedoing || _currentBatch == null) return;
        if (_currentBatch.Changes.Count > 0)
        {
            _undoStack.Push(_currentBatch);
            _redoStack.Clear(); // new change invalidates redo
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        _currentBatch = null;
    }

    /// <summary>Discard the current batch without committing.</summary>
    public void DiscardBatch()
    {
        _currentBatch = null;
    }

    /// <summary>Record a property change. Call this BEFORE changing the value.</summary>
    public void RecordPropertyChange(object target, string propertyName, object? oldValue, object? newValue)
    {
        if (_isUndoRedoing) return;

        var change = new PropertyChange(target, propertyName, oldValue, newValue);

        if (_currentBatch != null)
        {
            // Merge consecutive changes to same property
            var existing = _currentBatch.Changes
                .OfType<PropertyChange>()
                .LastOrDefault(c => c.Target == target && c.PropertyName == propertyName);
            if (existing != null)
            {
                existing.NewValue = newValue;
                return;
            }
            _currentBatch.Changes.Add(change);
        }
        else
        {
            // Auto-batch: single change = single batch
            var batch = new UndoBatch($"Edit {propertyName}");
            batch.Changes.Add(change);
            _undoStack.Push(batch);
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Record an item addition to a collection.</summary>
    public void RecordAdd<T>(IList<T> collection, T item, string description)
    {
        if (_isUndoRedoing) return;
        var change = new CollectionAddChange<T>(collection, item);
        var batch = new UndoBatch(description);
        batch.Changes.Add(change);
        _undoStack.Push(batch);
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Record an item removal from a collection.</summary>
    public void RecordRemove<T>(IList<T> collection, T item, int index, string description)
    {
        if (_isUndoRedoing) return;
        var change = new CollectionRemoveChange<T>(collection, item, index);
        var batch = new UndoBatch(description);
        batch.Changes.Add(change);
        _undoStack.Push(batch);
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _isUndoRedoing = true;
        try
        {
            var batch = _undoStack.Pop();
            // Undo in reverse order
            for (int i = batch.Changes.Count - 1; i >= 0; i--)
                batch.Changes[i].Undo();
            _redoStack.Push(batch);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally { _isUndoRedoing = false; }
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _isUndoRedoing = true;
        try
        {
            var batch = _redoStack.Pop();
            foreach (var change in batch.Changes)
                change.Redo();
            _undoStack.Push(batch);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally { _isUndoRedoing = false; }
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentBatch = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class UndoBatch
{
    public string Description { get; }
    public List<IUndoChange> Changes { get; } = new();
    public UndoBatch(string description) => Description = description;
}

public interface IUndoChange
{
    void Undo();
    void Redo();
}

public class PropertyChange : IUndoChange
{
    public object Target { get; }
    public string PropertyName { get; }
    public object? OldValue { get; }
    public object? NewValue { get; set; }

    public PropertyChange(object target, string propertyName, object? oldValue, object? newValue)
    {
        Target = target;
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public void Undo()
    {
        var prop = Target.GetType().GetProperty(PropertyName);
        prop?.SetValue(Target, OldValue);
    }

    public void Redo()
    {
        var prop = Target.GetType().GetProperty(PropertyName);
        prop?.SetValue(Target, NewValue);
    }
}

public class CollectionAddChange<T> : IUndoChange
{
    private readonly IList<T> _collection;
    private readonly T _item;
    public CollectionAddChange(IList<T> collection, T item) { _collection = collection; _item = item; }
    public void Undo() => _collection.Remove(_item);
    public void Redo() => _collection.Add(_item);
}

public class CollectionRemoveChange<T> : IUndoChange
{
    private readonly IList<T> _collection;
    private readonly T _item;
    private readonly int _index;
    public CollectionRemoveChange(IList<T> collection, T item, int index) { _collection = collection; _item = item; _index = index; }
    public void Undo() => _collection.Insert(Math.Min(_index, _collection.Count), _item);
    public void Redo() => _collection.Remove(_item);
}
