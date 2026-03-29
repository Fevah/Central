using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace Central.Desktop;

/// <summary>
/// Bulk edit dialog — select a field, enter a value, apply to all selected rows.
/// Works with any model type via reflection.
/// </summary>
public partial class BulkEditWindow : DevExpress.Xpf.Core.DXDialogWindow
{
    private readonly IReadOnlyList<object> _items;
    private readonly PropertyInfo[] _editableProps;

    /// <summary>Number of items actually modified.</summary>
    public int ModifiedCount { get; private set; }

    /// <summary>The field that was modified.</summary>
    public string? ModifiedField { get; private set; }

    public BulkEditWindow(IReadOnlyList<object> selectedItems)
    {
        _items = selectedItems;
        InitializeComponent();

        CountLabel.Text = $"Edit {_items.Count} selected rows";

        // Get writable string/bool/int properties
        var itemType = _items.FirstOrDefault()?.GetType();
        _editableProps = itemType?.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.CanRead
                && (p.PropertyType == typeof(string) || p.PropertyType == typeof(bool) || p.PropertyType == typeof(int))
                && p.Name != "Id" && !p.Name.StartsWith("Is") && !p.Name.EndsWith("Color"))
            .OrderBy(p => p.Name)
            .ToArray() ?? Array.Empty<PropertyInfo>();

        FieldCombo.ItemsSource = _editableProps.Select(p => p.Name).ToList();
    }

    private void FieldCombo_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        var fieldName = FieldCombo.EditValue as string;
        if (string.IsNullOrEmpty(fieldName)) return;

        var prop = _editableProps.FirstOrDefault(p => p.Name == fieldName);
        if (prop == null) return;

        // Show current values as preview
        var currentValues = _items
            .Select(i => prop.GetValue(i)?.ToString() ?? "(null)")
            .Distinct()
            .Take(5);
        PreviewText.Text = $"Current values: {string.Join(", ", currentValues)}" +
            (_items.Count > 5 ? "..." : "");
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var fieldName = FieldCombo.EditValue as string;
        var newValue = ValueEdit.Text;
        if (string.IsNullOrEmpty(fieldName)) return;

        var prop = _editableProps.FirstOrDefault(p => p.Name == fieldName);
        if (prop == null) return;

        int count = 0;
        foreach (var item in _items)
        {
            try
            {
                object? converted = prop.PropertyType switch
                {
                    Type t when t == typeof(bool) => bool.TryParse(newValue, out var b) && b,
                    Type t when t == typeof(int) => int.TryParse(newValue, out var i) ? i : 0,
                    _ => newValue
                };
                prop.SetValue(item, converted);
                count++;
            }
            catch { }
        }

        ModifiedCount = count;
        ModifiedField = fieldName;
        DialogResult = true;
    }
}
