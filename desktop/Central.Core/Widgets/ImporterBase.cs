using System.Collections.ObjectModel;

namespace Central.Core.Widgets;

/// <summary>
/// Base class for data import operations. Provides:
/// - Row-level validation with error collection
/// - Progress reporting (completed/total)
/// - Error navigation (First/Next/Prev/Last)
/// - Cancel support
/// - Summary report
///
/// Modules implement MapRow() and ValidateRow().
/// The engine provides the UI (progress bar, error grid, navigation buttons).
///
/// Based on TotalLink's ImporterViewModelBase<T> + UploaderViewModelBase<T>.
/// </summary>
public abstract class ImporterBase<T> where T : class, new()
{
    // ── State ──

    public ObservableCollection<ImportRow<T>> Rows { get; } = new();
    public ObservableCollection<ImportError> Errors { get; } = new();

    private int _completedCount;
    public int CompletedCount { get => _completedCount; private set { _completedCount = value; ProgressChanged?.Invoke(); } }
    public int TotalCount => Rows.Count;
    public double ProgressPercent => TotalCount > 0 ? (double)CompletedCount / TotalCount * 100 : 0;

    private bool _isCancelled;
    public bool IsCancelled => _isCancelled;

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set { _isRunning = value; ProgressChanged?.Invoke(); } }

    public event Action? ProgressChanged;

    // ── Abstract — module implements ──

    /// <summary>Map a raw dictionary (column→value from CSV/Excel) to a typed entity.</summary>
    protected abstract T MapRow(Dictionary<string, string> rawRow);

    /// <summary>Validate a row. Return null if valid, or an error message.</summary>
    protected abstract string? ValidateRow(T item, int rowIndex);

    /// <summary>Save a single validated item to the database.</summary>
    protected abstract Task SaveItemAsync(T item);

    // ── Column Aliases ──

    /// <summary>Column aliases — maps alternative header names to canonical property names.
    /// E.g., {"Device Name" → "SwitchName", "IP Address" → "PrimaryIp", "Bldg" → "Building"}</summary>
    public Dictionary<string, string> ColumnAliases { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolve a column header to its canonical name using aliases.</summary>
    protected string ResolveColumnName(string header)
    {
        if (ColumnAliases.TryGetValue(header, out var canonical))
            return canonical;
        // Try normalised match (strip spaces, underscores, dashes)
        var normalised = header.Replace(" ", "").Replace("_", "").Replace("-", "");
        foreach (var (alias, target) in ColumnAliases)
        {
            if (string.Equals(alias.Replace(" ", "").Replace("_", "").Replace("-", ""),
                normalised, StringComparison.OrdinalIgnoreCase))
                return target;
        }
        return header;
    }

    /// <summary>Get a value from a row dictionary, checking aliases.</summary>
    protected string GetValue(Dictionary<string, string> row, string propertyName)
    {
        if (row.TryGetValue(propertyName, out var val)) return val;
        // Check if any key in the row resolves to this property via alias
        foreach (var (key, value) in row)
            if (string.Equals(ResolveColumnName(key), propertyName, StringComparison.OrdinalIgnoreCase))
                return value;
        return "";
    }

    // ── Engine operations ──

    /// <summary>Load raw data (from CSV/Excel parser) and map + validate all rows.</summary>
    public void LoadAndValidate(List<Dictionary<string, string>> rawRows)
    {
        Rows.Clear();
        Errors.Clear();
        CompletedCount = 0;

        for (int i = 0; i < rawRows.Count; i++)
        {
            try
            {
                var item = MapRow(rawRows[i]);
                var error = ValidateRow(item, i);
                var row = new ImportRow<T>(i + 1, item, error);
                Rows.Add(row);
                if (error != null)
                    Errors.Add(new ImportError(i + 1, error, rawRows[i]));
            }
            catch (Exception ex)
            {
                Rows.Add(new ImportRow<T>(i + 1, new T(), ex.Message));
                Errors.Add(new ImportError(i + 1, ex.Message, rawRows[i]));
            }
        }
    }

    /// <summary>Import all valid rows. Skips rows with validation errors.</summary>
    public async Task ImportAsync()
    {
        _isCancelled = false;
        IsRunning = true;
        CompletedCount = 0;

        foreach (var row in Rows)
        {
            if (_isCancelled) break;
            if (row.HasError) { CompletedCount++; continue; }

            try
            {
                await SaveItemAsync(row.Item);
                row.Status = ImportStatus.Success;
            }
            catch (Exception ex)
            {
                row.Status = ImportStatus.Failed;
                row.Error = ex.Message;
                Errors.Add(new ImportError(row.RowNumber, ex.Message, null));
            }
            CompletedCount++;
        }

        IsRunning = false;
    }

    /// <summary>Cancel the import operation.</summary>
    public void Cancel() => _isCancelled = true;

    /// <summary>Summary: total, succeeded, failed, skipped.</summary>
    public ImportSummary GetSummary() => new(
        Total: Rows.Count,
        Succeeded: Rows.Count(r => r.Status == ImportStatus.Success),
        Failed: Rows.Count(r => r.Status == ImportStatus.Failed),
        Skipped: Rows.Count(r => r.HasError && r.Status == ImportStatus.Pending)
    );
}

public enum ImportStatus { Pending, Success, Failed }

public class ImportRow<T>
{
    public int RowNumber { get; }
    public T Item { get; }
    public string? Error { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Pending;
    public bool HasError => !string.IsNullOrEmpty(Error);
    public bool IsValid => !HasError;

    public ImportRow(int rowNumber, T item, string? error)
    {
        RowNumber = rowNumber;
        Item = item;
        Error = error;
    }
}

public record ImportError(int RowNumber, string Message, Dictionary<string, string>? RawData);
public record ImportSummary(int Total, int Succeeded, int Failed, int Skipped);
