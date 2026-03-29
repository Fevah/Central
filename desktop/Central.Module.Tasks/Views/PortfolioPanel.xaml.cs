using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Central.Core.Models;

namespace Central.Module.Tasks.Views;

/// <summary>Flat tree node for portfolio hierarchy roll-up.</summary>
public class PortfolioTreeNode
{
    public string TreeId { get; set; } = "";
    public string? TreeParentId { get; set; }
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";  // Portfolio, Programme, Project
    public int TaskCount { get; set; }
    public decimal TotalPoints { get; set; }
    public string CompletedPct { get; set; } = "";
    public int OpenBugs { get; set; }
    public int ActiveSprints { get; set; }
}

public partial class PortfolioPanel : System.Windows.Controls.UserControl
{
    public PortfolioPanel() => InitializeComponent();

    public DevExpress.Xpf.Grid.TreeListControl Tree => PortfolioTree;

    public event Func<Task<List<PortfolioTreeNode>>>? LoadPortfolio;

    public void LoadData(List<PortfolioTreeNode> nodes)
        => PortfolioTree.ItemsSource = nodes;

    private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (LoadPortfolio != null)
        {
            var nodes = await LoadPortfolio();
            PortfolioTree.ItemsSource = nodes;
        }
    }
}
