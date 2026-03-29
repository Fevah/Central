using DevExpress.Xpf.Grid;

namespace Central.Desktop.Services;

/// <summary>
/// Engine-level helper: double-click an icon column cell in any grid → opens ImagePickerWindow.
/// Returns the selected icon name/ID to the caller for saving.
///
/// Usage:
///   IconCellHelper.Attach(myGrid, myView, "IconName", App.Dsn, this);
/// </summary>
public static class IconCellHelper
{
    /// <summary>
    /// Attach a double-click handler to a specific column that opens the image picker.
    /// When an icon is selected, sets the cell value to the icon name.
    /// </summary>
    /// <param name="grid">The GridControl</param>
    /// <param name="view">The TableView</param>
    /// <param name="fieldName">The column FieldName to handle (e.g. "IconName", "Glyph")</param>
    /// <param name="dsn">DB connection string for the picker</param>
    /// <param name="owner">Owner window for the picker dialog</param>
    public static void Attach(GridControl grid, TableView view, string fieldName,
        string dsn, System.Windows.Window owner)
    {
        view.RowDoubleClick += (sender, e) =>
        {
            if (view.FocusedColumn?.FieldName != fieldName) return;

            var picker = new ImagePickerWindow(dsn) { Owner = owner };
            if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedIconName))
            {
                // Set the cell value
                grid.SetCellValue(view.FocusedRowHandle, fieldName, picker.SelectedIconName);
            }
        };
    }

    /// <summary>
    /// Attach with a custom callback instead of setting cell value directly.
    /// Useful when you need to save to DB or update a different property.
    /// </summary>
    public static void Attach(GridControl grid, TableView view, string fieldName,
        string dsn, System.Windows.Window owner, Action<int, string, int> onIconSelected)
    {
        view.RowDoubleClick += (sender, e) =>
        {
            if (view.FocusedColumn?.FieldName != fieldName) return;

            var picker = new ImagePickerWindow(dsn) { Owner = owner };
            if (picker.ShowDialog() == true)
            {
                onIconSelected(view.FocusedRowHandle, picker.SelectedIconName, picker.SelectedIconId);
            }
        };
    }
}
