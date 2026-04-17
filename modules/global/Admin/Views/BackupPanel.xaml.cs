using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

/// <summary>
/// Backup panel — backup controls at top, history grid below.
/// </summary>
public partial class BackupPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<BackupRecord> History { get; } = new();

    /// <summary>Delegate to run a backup. Receives the output file path.</summary>
    public Func<string, Task>? RunBackup { get; set; }

    /// <summary>Delegate to refresh the backup history.</summary>
    public Func<Task>? RefreshRequested { get; set; }

    public BackupPanel()
    {
        InitializeComponent();
        BackupGrid.ItemsSource = History;
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => BackupGrid;
    public TableView View => BackupView;

    /// <summary>Expose status label for host to update.</summary>
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    /// <summary>Get or set the output path.</summary>
    public string OutputPath
    {
        get => OutputPathEdit.Text ?? "";
        set => OutputPathEdit.Text = value;
    }

    /// <summary>Load backup history records into the grid.</summary>
    public void Load(IEnumerable<BackupRecord> items)
    {
        History.Clear();
        foreach (var item in items)
            History.Add(item);
        StatusLabel.Text = $"{History.Count} backup(s)";
    }

    private async void RunBackup_Click(object sender, RoutedEventArgs e)
    {
        var path = OutputPathEdit.Text;
        if (string.IsNullOrWhiteSpace(path))
        {
            System.Windows.MessageBox.Show(
                "Please select an output path first.",
                "Backup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (RunBackup != null)
            await RunBackup.Invoke(path);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select Backup Output Path",
            Filter = "SQL Files (*.sql)|*.sql|All Files (*.*)|*.*",
            DefaultExt = ".sql",
            FileName = $"central_backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql"
        };
        if (dlg.ShowDialog() == true)
            OutputPathEdit.Text = dlg.FileName;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null)
            await RefreshRequested.Invoke();
    }
}
