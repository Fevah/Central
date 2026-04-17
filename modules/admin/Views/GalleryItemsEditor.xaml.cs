using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Central.Engine.Services;
using DevExpress.Xpf.Core;

namespace Central.Module.Admin.Views;

/// <summary>
/// Dialog for authoring the tile list of a ribbon gallery — add, remove,
/// reorder tiles, and pick a per-tile icon. Serialises back to the pipe-
/// separated string (<c>Caption|IconName;Caption;...</c>) consumed by
/// <see cref="GalleryItemParser"/> and stored in
/// <c>RibbonTreeItem.DropdownItems</c>.
/// </summary>
public partial class GalleryItemsEditor : DXDialogWindow
{
    public ObservableCollection<GalleryTileRow> Tiles { get; } = new();

    /// <summary>Serialised result string (pipe+semicolon syntax). Null if cancelled.</summary>
    public string? Result { get; private set; }

    /// <summary>Shell-supplied icon picker delegate (same as the admin tree uses).</summary>
    public Func<string?>? OpenIconPicker { get; set; }

    /// <summary>Shell-supplied icon renderer (DX name → ImageSource).</summary>
    public Func<string, object?>? RenderIconPreview { get; set; }

    public GalleryItemsEditor(string? currentValue)
    {
        InitializeComponent();
        // Hydrate from the existing pipe-separated string.
        foreach (var tile in GalleryItemParser.Parse(currentValue))
            Tiles.Add(new GalleryTileRow { Caption = tile.Caption, IconName = tile.IconName ?? "" });
        TilesGrid.ItemsSource = Tiles;
    }

    public void RefreshPreviews()
    {
        if (RenderIconPreview == null) return;
        foreach (var t in Tiles)
            if (!string.IsNullOrEmpty(t.IconName))
                t.IconPreview = RenderIconPreview(t.IconName) as System.Windows.Media.ImageSource;
    }

    private void PickTileIcon_Click(object sender, RoutedEventArgs e)
    {
        if (OpenIconPicker == null) return;
        var row = TilesGrid.GetRow(TilesGrid.View.FocusedRowHandle) as GalleryTileRow;
        if (row == null) return;
        var icon = OpenIconPicker();
        if (!string.IsNullOrEmpty(icon))
        {
            row.IconName = icon;
            row.IconPreview = RenderIconPreview?.Invoke(icon) as System.Windows.Media.ImageSource;
        }
    }

    private void ClearTileIcon_Click(object sender, RoutedEventArgs e)
    {
        if (TilesGrid.GetRow(TilesGrid.View.FocusedRowHandle) is GalleryTileRow row)
        {
            row.IconName = "";
            row.IconPreview = null;
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int dir)
    {
        var idx = TilesGrid.View.FocusedRowHandle;
        if (idx < 0 || idx >= Tiles.Count) return;
        var newIdx = idx + dir;
        if (newIdx < 0 || newIdx >= Tiles.Count) return;
        Tiles.Move(idx, newIdx);
        TilesGrid.View.FocusedRowHandle = newIdx;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var idx = TilesGrid.View.FocusedRowHandle;
        if (idx >= 0 && idx < Tiles.Count) Tiles.RemoveAt(idx);
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        // Serialise tiles back to the pipe+semicolon string.
        var parts = new List<string>();
        foreach (var t in Tiles)
        {
            if (string.IsNullOrWhiteSpace(t.Caption)) continue;
            var entry = string.IsNullOrEmpty(t.IconName) ? t.Caption : $"{t.Caption}|{t.IconName}";
            parts.Add(entry);
        }
        Result = string.Join("; ", parts);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }
}

public sealed class GalleryTileRow : INotifyPropertyChanged
{
    private string _caption = "";
    private string _iconName = "";
    private System.Windows.Media.ImageSource? _iconPreview;

    public string Caption
    {
        get => _caption;
        set { if (_caption != value) { _caption = value; N(); } }
    }
    public string IconName
    {
        get => _iconName;
        set { if (_iconName != value) { _iconName = value; N(); } }
    }
    public System.Windows.Media.ImageSource? IconPreview
    {
        get => _iconPreview;
        set { _iconPreview = value; N(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
