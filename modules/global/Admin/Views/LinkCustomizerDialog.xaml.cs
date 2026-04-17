using System.Collections.ObjectModel;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

/// <summary>
/// Dialog for configuring cross-panel link rules.
/// Users define: when I select in Panel A, filter Panel B by matching field.
/// Rules are stored in panel_customizations and applied by the LinkEngine.
/// </summary>
public partial class LinkCustomizerDialog : DevExpress.Xpf.Core.DXWindow
{
    public LinkCustomizerDialog() => InitializeComponent();

    public ObservableCollection<LinkRule> Rules { get; } = new();

    /// <summary>Set available panel names for the dropdowns.</summary>
    public void SetPanelNames(IEnumerable<string> panelNames)
    {
        var items = panelNames.ToList();
        SourcePanelCombo.ItemsSource = items;
        TargetPanelCombo.ItemsSource = items;
    }

    public void Load(IEnumerable<LinkRule> rules)
    {
        Rules.Clear();
        foreach (var r in rules) Rules.Add(r);
        RulesGrid.ItemsSource = Rules;
        StatusLabel.Text = $"{Rules.Count} link rules";
    }

    private void AddRule_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var rule = new LinkRule { FilterOnSelect = true };
        Rules.Add(rule);
        RulesGrid.SelectedItem = rule;
    }

    private void DeleteRule_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is LinkRule rule)
            Rules.Remove(rule);
    }

    private void Apply_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
