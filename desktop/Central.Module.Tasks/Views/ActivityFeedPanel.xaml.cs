using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Central.Core.Models;

namespace Central.Module.Tasks.Views;

public partial class ActivityFeedPanel : System.Windows.Controls.UserControl
{
    public ActivityFeedPanel() => InitializeComponent();

    public event Func<int?, Task<List<ActivityFeedItem>>>? LoadFeed;
    public event Func<int?, Task>? ProjectChanged;

    public void SetProjects(IEnumerable<TaskProject> projects)
    {
        var items = new List<TaskProject> { new() { Id = 0, Name = "(All Projects)" } };
        items.AddRange(projects);
        ProjectSelector.ItemsSource = items;
    }

    public int? SelectedProjectId =>
        ProjectSelector.EditValue is TaskProject p && p.Id > 0 ? p.Id : null;

    public void LoadItems(List<ActivityFeedItem> items) => FeedList.ItemsSource = items;

    private void ProjectSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => ProjectChanged?.Invoke(SelectedProjectId);

    private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (LoadFeed != null)
        {
            var items = await LoadFeed(SelectedProjectId);
            FeedList.ItemsSource = items;
        }
    }
}
