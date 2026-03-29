using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Admin.Views;

/// <summary>
/// Migrations panel — read-only grid showing database migration status with apply/refresh actions.
/// </summary>
public partial class MigrationsPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<MigrationRecord> Items { get; } = new();

    /// <summary>Delegate to apply all pending migrations.</summary>
    public Func<Task>? ApplyPending { get; set; }

    /// <summary>Delegate to refresh the migrations list.</summary>
    public Func<Task>? RefreshRequested { get; set; }

    public MigrationsPanel()
    {
        InitializeComponent();
        MigrationsGrid.ItemsSource = Items;
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => MigrationsGrid;
    public TableView View => MigrationsView;

    /// <summary>Expose status label for host to update.</summary>
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    /// <summary>Load migration records into the grid.</summary>
    public void Load(IEnumerable<MigrationRecord> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        var pending = 0;
        var applied = 0;
        foreach (var item in Items)
        {
            if (item.IsApplied) applied++;
            else pending++;
        }
        StatusLabel.Text = $"{Items.Count} migration(s) — {applied} applied, {pending} pending";
    }

    private async void ApplyPending_Click(object sender, RoutedEventArgs e)
    {
        if (ApplyPending != null)
            await ApplyPending.Invoke();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null)
            await RefreshRequested.Invoke();
    }
}
