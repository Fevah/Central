using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

/// <summary>
/// Podman panel — grid showing containers with start/stop/restart/logs actions.
/// </summary>
public partial class PodmanPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<ContainerInfo> Containers { get; } = new();

    /// <summary>Delegate to refresh the container list.</summary>
    public Func<Task>? RefreshContainers { get; set; }

    /// <summary>Delegate to start a container by name.</summary>
    public Func<string, Task>? StartContainer { get; set; }

    /// <summary>Delegate to stop a container by name.</summary>
    public Func<string, Task>? StopContainer { get; set; }

    /// <summary>Delegate to restart a container by name.</summary>
    public Func<string, Task>? RestartContainer { get; set; }

    /// <summary>Delegate to get logs for a container by name. Returns log text.</summary>
    public Func<string, Task<string>>? GetLogs { get; set; }

    public PodmanPanel()
    {
        InitializeComponent();
        ContainersGrid.ItemsSource = Containers;
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => ContainersGrid;
    public TableView View => ContainersView;

    /// <summary>Expose status label for host to update.</summary>
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    /// <summary>The currently selected container.</summary>
    public ContainerInfo? SelectedContainer => ContainersGrid.CurrentItem as ContainerInfo;

    /// <summary>Load container info into the grid.</summary>
    public void Load(IEnumerable<ContainerInfo> containers)
    {
        Containers.Clear();
        foreach (var c in containers)
            Containers.Add(c);

        var running = 0;
        foreach (var c in Containers)
            if (c.IsRunning) running++;
        StatusLabel.Text = $"{Containers.Count} container(s), {running} running";
    }

    /// <summary>Append text to the log output area.</summary>
    public void AppendLog(string text)
    {
        LogOutput.Text += text + "\n";
        LogOutput.ScrollToEnd();
    }

    /// <summary>Clear the log output area.</summary>
    public void ClearLog()
    {
        LogOutput.Text = "";
    }

    private string? GetSelectedName()
    {
        var container = SelectedContainer;
        if (container == null)
        {
            System.Windows.MessageBox.Show(
                "Select a container first.", "Podman",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        return container.Name;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshContainers != null)
            await RefreshContainers.Invoke();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var name = GetSelectedName();
        if (name != null && StartContainer != null)
            await StartContainer.Invoke(name);
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        var name = GetSelectedName();
        if (name != null && StopContainer != null)
            await StopContainer.Invoke(name);
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        var name = GetSelectedName();
        if (name != null && RestartContainer != null)
            await RestartContainer.Invoke(name);
    }

    private async void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        var name = GetSelectedName();
        if (name != null && GetLogs != null)
        {
            ClearLog();
            var logs = await GetLogs.Invoke(name);
            LogOutput.Text = logs;
            LogOutput.ScrollToEnd();
        }
    }
}
