using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Central.Engine.Auth;
using Central.Engine.Services;
using Central.Persistence;
using Central.Desktop.Services;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;

namespace Central.Desktop.Views;

/// <summary>
/// Admin dialog for editing per-module overrides for the 8 global ribbon actions.
/// Writes rows to <c>admin_ribbon_defaults</c> keyed as <c>"{module}/{action}"</c>
/// (e.g. "devices/new", "global/delete"). The "global" pseudo-module acts as the
/// root fallback — used when a specific module has no entry. Hardcoded defaults
/// in <see cref="GlobalActionService.DefaultIconGlyph"/> remain the final fallback.
/// </summary>
public partial class GlobalActionsAdminDialog : DXDialogWindow
{
    // Modules shown in the matrix — must match the keys GlobalActionService understands.
    private static readonly string[] Modules =
    {
        "global", "home", "devices", "switches", "links",
        "bgp", "vlans", "admin", "tasks", "servicedesk"
    };

    private readonly DbRepository _repo;
    public ObservableCollection<GlobalActionAdminRow> Rows { get; } = new();
    private bool _showOnlyCustom;

    public GlobalActionsAdminDialog(DbRepository repo)
    {
        _repo = repo;
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Rows.Clear();
        var map = await _repo.GetGlobalActionOverrideMapAsync(AuthContext.Instance.CurrentUser?.Id);

        foreach (var module in Modules)
            foreach (var action in GlobalActionService.AllActions)
            {
                var defaultGlyph = GlobalActionService.DefaultIconGlyph(action);
                map.TryGetValue((module, action), out var ov);
                var row = new GlobalActionAdminRow
                {
                    Module        = module,
                    Action        = action,
                    DefaultIcon   = defaultGlyph,
                    DefaultPreview= MainWindow.ResolveGlyphImageStatic(defaultGlyph),
                    CustomIcon    = ov.Icon ?? "",
                    CustomPreview = string.IsNullOrEmpty(ov.Icon) ? null : MainWindow.ResolveGlyphImageStatic(ov.Icon),
                    IsHidden      = ov.IsVisible == false,
                    IsDirty       = false
                };
                row.PropertyChanged += Row_PropertyChanged;
                Rows.Add(row);
            }

        ApplyFilter();
        RowsGrid.ItemsSource = Rows;
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GlobalActionAdminRow row) return;
        if (e.PropertyName is nameof(GlobalActionAdminRow.CustomIcon) or nameof(GlobalActionAdminRow.IsHidden))
        {
            row.IsDirty = true;
            // Refresh preview when icon text changes (keyboard-entered DX image name)
            if (e.PropertyName == nameof(GlobalActionAdminRow.CustomIcon))
                row.CustomPreview = string.IsNullOrEmpty(row.CustomIcon)
                    ? null
                    : MainWindow.ResolveGlyphImageStatic(row.CustomIcon);
        }
    }

    private void ApplyFilter()
    {
        if (!_showOnlyCustom) { RowsGrid.FilterString = ""; return; }
        RowsGrid.FilterString = "[IsDirty] = True Or Not IsNullOrEmpty([CustomIcon]) Or [IsHidden] = True";
    }

    private void OnlyCustom_Changed(object sender, RoutedEventArgs e)
    {
        _showOnlyCustom = OnlyCustomCheckBox.IsChecked == true;
        ApplyFilter();
    }

    private void PickIcon_Click(object sender, RoutedEventArgs e)
    {
        var row = GetFocusedRow();
        if (row == null) return;

        var dsn = Environment.GetEnvironmentVariable("CENTRAL_DSN")
            ?? "Host=localhost;Port=5432;Database=central;Username=central;Password=central";
        var picker = new ImagePickerWindow(dsn) { Owner = this };
        if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedIconName))
        {
            // The picker returns a DB icon library name — use it as the glyph. Our resolver
            // checks the icon_library first, then DX images.
            row.CustomIcon = picker.SelectedIconName;
        }
    }

    private void ClearIcon_Click(object sender, RoutedEventArgs e)
    {
        var row = GetFocusedRow();
        if (row != null) row.CustomIcon = "";
    }

    private GlobalActionAdminRow? GetFocusedRow()
        => RowsGrid.GetRow(RowsGrid.View.FocusedRowHandle) as GlobalActionAdminRow;

    private void ResetRow_Click(object sender, RoutedEventArgs e)
    {
        if (RowsGrid.View.FocusedRow is not GlobalActionAdminRow row) return;
        row.CustomIcon = "";
        row.IsHidden = false;
        row.IsDirty = true;
    }

    private async void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
        if (userId == 0)
        {
            DXMessageBox.Show("No user context — cannot save.", "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int saved = 0;
        foreach (var row in Rows.Where(r => r.IsDirty))
        {
            var key = $"{row.Module}/{row.Action}";
            var icon = string.IsNullOrWhiteSpace(row.CustomIcon) ? null : row.CustomIcon;
            try
            {
                await _repo.UpsertAdminRibbonDefaultAsync(key, icon, null, row.IsHidden, userId);
                row.IsDirty = false;
                saved++;
            }
            catch (Exception ex)
            {
                Central.Persistence.AppLogger.LogException("Ribbon", ex, $"SaveAll row {key}");
            }
        }

        // Reload the live override map + refresh ribbon injection so changes show immediately.
        if (Owner is MainWindow mw) await mw.ReloadGlobalActionOverridesAsync();

        NotificationService.Instance.Success($"Saved {saved} override(s)");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

/// <summary>Row in the global-action override admin grid.</summary>
public sealed class GlobalActionAdminRow : INotifyPropertyChanged
{
    private string _customIcon = "";
    private bool _isHidden;
    private bool _isDirty;
    private System.Windows.Media.ImageSource? _customPreview;

    public string Module       { get; set; } = "";
    public string Action       { get; set; } = "";
    public string DefaultIcon  { get; set; } = "";
    public System.Windows.Media.ImageSource? DefaultPreview { get; set; }

    public string CustomIcon
    {
        get => _customIcon;
        set { if (_customIcon != value) { _customIcon = value; OnPropertyChanged(); } }
    }
    public System.Windows.Media.ImageSource? CustomPreview
    {
        get => _customPreview;
        set { _customPreview = value; OnPropertyChanged(); }
    }
    public bool IsHidden
    {
        get => _isHidden;
        set { if (_isHidden != value) { _isHidden = value; OnPropertyChanged(); } }
    }
    public bool IsDirty
    {
        get => _isDirty;
        set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
