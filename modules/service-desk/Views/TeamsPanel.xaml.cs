using System.Collections.ObjectModel;
using System.ComponentModel;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.ServiceDesk.Views;

public partial class TeamsPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<TeamRow> Teams { get; } = new();
    private List<string> _allTechs = new();

    /// <summary>Called when a team needs saving (add/edit).</summary>
    public Func<SdTeam, Task<int>>? SaveTeam { get; set; }
    /// <summary>Called when a team needs deleting.</summary>
    public Func<int, Task>? DeleteTeam { get; set; }

    public TeamsPanel()
    {
        InitializeComponent();
        TeamsGrid.ItemsSource = Teams;
    }

    public void LoadTechnicians(List<string> names)
    {
        _allTechs = names;
    }

    public void LoadTeams(List<SdTeam> teams)
    {
        Teams.Clear();
        foreach (var t in teams)
            Teams.Add(new TeamRow { Id = t.Id, Name = t.Name, SortOrder = t.SortOrder, Members = t.Members });
    }

    private void TeamsGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        if (TeamsGrid.CurrentItem is TeamRow row)
        {
            SelectedTeamLabel.Text = $"Members of: {row.Name}";
            // Build checked list of all techs, pre-checking those in the team
            var items = _allTechs.Select(t => new TechCheck { Name = t, IsChecked = row.Members.Contains(t) }).ToList();
            MembersList.ItemsSource = items;
            MembersList.DisplayMember = "Name";
            // Set checked items
            var checkedValues = items.Where(i => i.IsChecked).Select(i => (object)i).ToList();
            MembersList.EditValue = checkedValues;

            // Wire change event — save members when checkboxes change
            MembersList.EditValueChanged -= MembersList_Changed;
            MembersList.EditValueChanged += MembersList_Changed;
        }
        else
        {
            SelectedTeamLabel.Text = "Select a team to assign members";
            MembersList.ItemsSource = null;
        }
    }

    private async void MembersList_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        if (TeamsGrid.CurrentItem is not TeamRow row) return;

        // Get checked items
        var checkedItems = new List<string>();
        if (MembersList.EditValue is System.Collections.IList list)
        {
            foreach (var item in list)
            {
                if (item is TechCheck tc) checkedItems.Add(tc.Name);
                else if (item is string s) checkedItems.Add(s);
            }
        }

        row.Members = checkedItems;

        if (SaveTeam != null && row.Id > 0)
        {
            await SaveTeam(new SdTeam { Id = row.Id, Name = row.Name, SortOrder = row.SortOrder, Members = row.Members });
        }
    }

    private async void TeamsGrid_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is TeamRow row && SaveTeam != null)
        {
            var team = new SdTeam { Id = row.Id, Name = row.Name, SortOrder = row.SortOrder, Members = row.Members };
            row.Id = await SaveTeam(team);
        }
    }

    private void AddTeam_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Teams.Add(new TeamRow { Name = "New Team", SortOrder = Teams.Count });
        TeamsGrid.CurrentItem = Teams.Last();
    }

    private async void DeleteTeam_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TeamsGrid.CurrentItem is TeamRow row)
        {
            if (row.Id > 0 && DeleteTeam != null) await DeleteTeam(row.Id);
            Teams.Remove(row);
        }
    }

    public class TeamRow : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private int _sortOrder;

        public int Id { get => _id; set { _id = value; N(); } }
        public string Name { get => _name; set { _name = value; N(); } }
        public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }
        public List<string> Members { get; set; } = new();
        public int MemberCount => Members.Count;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void N([System.Runtime.CompilerServices.CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class TechCheck
    {
        public string Name { get; set; } = "";
        public bool IsChecked { get; set; }
        public override string ToString() => Name;
    }
}
