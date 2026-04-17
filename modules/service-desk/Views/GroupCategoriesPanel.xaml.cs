using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.ServiceDesk.Views;

/// <summary>
/// Tree view: category nodes (parents) with ME group nodes (children).
/// Uncategorized ME groups show at root. Drag to reparent.
/// </summary>
public partial class GroupCategoriesPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<GroupTreeNode> Nodes { get; } = new();
    private Dictionary<string, int> _ticketCounts = new();

    public Func<SdGroupCategory, Task<int>>? SaveCategory { get; set; }
    public Func<int, Task>? DeleteCategory { get; set; }
    /// <summary>Called after any structural change so the caller can refresh settings panel.</summary>
    public Func<Task>? OnStructureChanged { get; set; }

    public GroupCategoriesPanel()
    {
        InitializeComponent();
        Tree.ItemsSource = Nodes;

        // No auto-save — use Save button to commit changes
    }

    public void LoadData(List<SdGroupCategory> categories, List<string> allGroups, Dictionary<string, int> ticketCounts)
    {
        _ticketCounts = ticketCounts;
        Nodes.Clear();

        int nextId = 1;
        var categorizedGroups = new HashSet<string>();

        // Category parent nodes + their group children
        foreach (var cat in categories)
        {
            var catNodeId = nextId++;
            var totalTickets = cat.Members.Sum(m => ticketCounts.GetValueOrDefault(m, 0));
            Nodes.Add(new GroupTreeNode
            {
                NodeId = catNodeId, ParentNodeId = 0,
                DisplayName = cat.Name, NodeType = "Category",
                IsActive = cat.IsActive, SortOrder = cat.SortOrder,
                CategoryId = cat.Id, TicketCount = totalTickets
            });

            foreach (var member in cat.Members)
            {
                categorizedGroups.Add(member);
                Nodes.Add(new GroupTreeNode
                {
                    NodeId = nextId++, ParentNodeId = catNodeId,
                    DisplayName = member, NodeType = "Group",
                    IsActive = true, CategoryId = cat.Id,
                    TicketCount = ticketCounts.GetValueOrDefault(member, 0)
                });
            }
        }

        // Uncategorized ME groups at root
        foreach (var g in allGroups.Where(g => !categorizedGroups.Contains(g)))
        {
            Nodes.Add(new GroupTreeNode
            {
                NodeId = nextId++, ParentNodeId = 0,
                DisplayName = g, NodeType = "Group",
                IsActive = true, CategoryId = 0,
                TicketCount = ticketCounts.GetValueOrDefault(g, 0)
            });
        }
    }

    /// <summary>Raised to show status messages.</summary>
    public event Action<string>? StatusMessage;

    /// <summary>Save all categories from the current tree state to DB, then refresh settings.</summary>
    public async Task SaveAllAsync()
    {
        if (SaveCategory == null)
        {
            StatusMessage?.Invoke("Save failed: delegate not wired");
            return;
        }

        // All category nodes (root level, NodeType=Category)
        var catNodes = Nodes.Where(n => n.NodeType == "Category" && n.ParentNodeId == 0).ToList();
        int saved = 0, errors = 0;

        int sortIdx = 0;
        foreach (var catNode in catNodes)
        {
            // Find all group children of this category
            var members = Nodes
                .Where(n => n.NodeType == "Group" && n.ParentNodeId == catNode.NodeId)
                .Select(n => n.DisplayName).ToList();

            var cat = new SdGroupCategory
            {
                Id = catNode.CategoryId,
                Name = catNode.DisplayName,
                SortOrder = sortIdx++,
                IsActive = catNode.IsActive,
                Members = members
            };

            try
            {
                var newId = await SaveCategory(cat);
                catNode.CategoryId = newId;
                saved++;
            }
            catch (Exception ex)
            {
                errors++;
                StatusMessage?.Invoke($"Error saving '{cat.Name}': {ex.Message}");
            }
        }

        if (errors == 0)
            StatusMessage?.Invoke($"Saved {saved} categories");
        else
            StatusMessage?.Invoke($"Saved {saved}, {errors} errors");

        // Refresh settings panel
        if (OnStructureChanged != null) await OnStructureChanged();
    }

    private void AddCategory_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var maxId = Nodes.Count > 0 ? Nodes.Max(n => n.NodeId) : 0;
        var node = new GroupTreeNode
        {
            NodeId = maxId + 1, ParentNodeId = 0,
            DisplayName = "New Category", NodeType = "Category",
            IsActive = true, SortOrder = Nodes.Count(n => n.NodeType == "Category")
        };
        Nodes.Add(node);
    }

    private async void Delete_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TreeView.FocusedRow is not GroupTreeNode node) return;

        if (node.NodeType == "Category")
        {
            // Move children to root
            var children = Nodes.Where(n => n.ParentNodeId == node.NodeId).ToList();
            foreach (var c in children) c.ParentNodeId = 0;
            // Delete from DB
            if (node.CategoryId > 0 && DeleteCategory != null) await DeleteCategory(node.CategoryId);
            Nodes.Remove(node);
        }
        else if (node.NodeType == "Group" && node.ParentNodeId > 0)
        {
            // Move group to root (unparent)
            node.ParentNodeId = 0;
        }

        // Save remaining structure + refresh settings
        await SaveAllAsync();
    }

    private void MoveUp_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TreeView.FocusedRow is GroupTreeNode node && node.NodeType == "Category")
        {
            var cats = Nodes.Where(n => n.NodeType == "Category" && n.ParentNodeId == 0).OrderBy(n => n.SortOrder).ToList();
            var idx = cats.IndexOf(node);
            if (idx > 0) { cats[idx].SortOrder--; cats[idx - 1].SortOrder++; }
            _ = SaveAllAsync();
        }
    }

    private async void Save_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SaveCategory == null)
        {
            System.Windows.MessageBox.Show("Save handler not wired — close and reopen this panel", "Error");
            return;
        }
        await SaveAllAsync();
    }

    private void MoveDown_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TreeView.FocusedRow is GroupTreeNode node && node.NodeType == "Category")
        {
            var cats = Nodes.Where(n => n.NodeType == "Category" && n.ParentNodeId == 0).OrderBy(n => n.SortOrder).ToList();
            var idx = cats.IndexOf(node);
            if (idx < cats.Count - 1) { cats[idx].SortOrder++; cats[idx + 1].SortOrder--; }
            _ = SaveAllAsync();
        }
    }

    // Only needed for LoadGroups backward compat
    public void LoadGroups(List<string> groups) { }
    public void LoadCategories(List<SdGroupCategory> cats) { }
}

/// <summary>Flat tree node for the group categories tree.</summary>
public class GroupTreeNode : INotifyPropertyChanged
{
    private int _nodeId, _parentNodeId, _sortOrder, _ticketCount, _categoryId;
    private string _displayName = "", _nodeType = "";
    private bool _isActive = true;

    public int NodeId { get => _nodeId; set { _nodeId = value; N(); } }
    public int ParentNodeId { get => _parentNodeId; set { _parentNodeId = value; N(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; N(); } }
    public string NodeType { get => _nodeType; set { _nodeType = value; N(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }
    public int TicketCount { get => _ticketCount; set { _ticketCount = value; N(); } }
    public int CategoryId { get => _categoryId; set { _categoryId = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
