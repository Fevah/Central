using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.ServiceDesk.Views;

public partial class RequestGridPanel : System.Windows.Controls.UserControl
{
    private static readonly string[] Statuses = { "Open", "In Progress", "On Hold", "Resolved", "Closed" };
    private static readonly string[] Priorities = { "Low", "Medium", "Normal", "High", "Urgent" };

    public RequestGridPanel()
    {
        InitializeComponent();
        BindCombos();
    }

    public GridControl Grid => RequestsGrid;
    public TableView View => RequestsView;

    /// <summary>Called to flush dirty rows to ManageEngine.</summary>
    public Func<List<SdRequest>, Task>? WriteBackDirty { get; set; }

    /// <summary>Bind combo dropdowns for editable columns (must be done in code — EditSettings not in visual tree).</summary>
    private void BindCombos()
    {
        ColStatus.EditSettings = new ComboBoxEditSettings { ItemsSource = Statuses, IsTextEditable = false };
        ColPriority.EditSettings = new ComboBoxEditSettings { ItemsSource = Priorities, IsTextEditable = false };
    }

    /// <summary>Bind group and technician dropdowns from DB data.</summary>
    public void BindLookups(List<string> groups, List<string> technicians, List<string> categories)
    {
        ColGroup.EditSettings = new ComboBoxEditSettings { ItemsSource = groups, IsTextEditable = true };
        ColTechnician.EditSettings = new ComboBoxEditSettings { ItemsSource = technicians, IsTextEditable = true };
        ColCategory.EditSettings = new ComboBoxEditSettings { ItemsSource = categories, IsTextEditable = true };
    }

    private void RequestsView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        UpdateDirtyStatus();
    }

    private void UpdateDirtyStatus()
    {
        var dirtyRows = GetDirtyRows();
        var count = dirtyRows.Count;
        if (count > 0)
        {
            DirtyCountLabel.Text = $"{count} unsaved change{(count > 1 ? "s" : "")}";
            SaveChangesBtn.Visibility = Visibility.Visible;
            DiscardChangesBtn.Visibility = Visibility.Visible;
        }
        else
        {
            DirtyCountLabel.Text = "";
            SaveChangesBtn.Visibility = Visibility.Collapsed;
            DiscardChangesBtn.Visibility = Visibility.Collapsed;
        }
    }

    private List<SdRequest> GetDirtyRows()
    {
        var dirty = new List<SdRequest>();
        if (RequestsGrid.ItemsSource is List<SdRequest> items)
            foreach (var r in items)
                if (r.IsDirty) dirty.Add(r);
        return dirty;
    }

    private async void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        var dirty = GetDirtyRows();
        if (dirty.Count == 0) return;

        SaveChangesBtn.IsEnabled = false;
        DirtyCountLabel.Text = $"Writing {dirty.Count} changes to ManageEngine...";

        try
        {
            if (WriteBackDirty != null)
                await WriteBackDirty(dirty);

            // After successful write-back, accept changes
            foreach (var r in dirty) r.AcceptChanges();
            UpdateDirtyStatus();
        }
        catch (Exception ex)
        {
            DirtyCountLabel.Text = $"Write-back error: {ex.Message}";
        }
        finally
        {
            SaveChangesBtn.IsEnabled = true;
        }
    }

    private void DiscardChanges_Click(object sender, RoutedEventArgs e)
    {
        var dirty = GetDirtyRows();
        foreach (var r in dirty)
        {
            r.Status = r.OriginalStatus;
            r.Priority = r.OriginalPriority;
            r.GroupName = r.OriginalGroupName;
            r.TechnicianName = r.OriginalTechnicianName;
            r.Category = r.OriginalCategory;
        }
        UpdateDirtyStatus();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri != null && !string.IsNullOrEmpty(e.Uri.AbsoluteUri))
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
