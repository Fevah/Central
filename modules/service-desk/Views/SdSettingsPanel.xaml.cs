using System.Windows;
using System.Windows.Media;
using Central.Engine.Models;
using WC = System.Windows.Controls;

namespace Central.Module.ServiceDesk.Views;

/// <summary>
/// Global SD settings/filter side panel. Drives all SD chart and grid panels.
/// Extensible — add new filter sections as needed.
/// </summary>
public partial class SdSettingsPanel : System.Windows.Controls.UserControl
{
    private bool _suppressEvents;

    public SdSettingsPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            try
            {
                RangeCombo.SelectedIndex = 0;
                ScaleCombo.SelectedIndex = 0;
                BarStyleCombo.SelectedIndex = 0;
                ChartThemeCombo.SelectedIndex = 0;
                ChartTypeCombo.SelectedIndex = 0;
                GridStyleCombo.SelectedIndex = 0;
            }
            catch { }
        };
    }

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Fired when any chart filter changes (range, techs, groups, overlays).</summary>
    public event Action? FiltersChanged;

    /// <summary>Fired when grid display options change.</summary>
    public event Action? GridOptionsChanged;

    // ── Filter state — single source of truth ──────────────────────────

    /// <summary>Build a complete SdFilterState from the current panel selections.</summary>
    public SdFilterState GetCurrentFilters()
    {
        var (start, end) = GetDateRange();
        return new SdFilterState
        {
            RangeStart = start,
            RangeEnd = end,
            Bucket = Bucket,
            SelectedTechs = GetSelectedTechs(),
            SelectedGroups = GetSelectedGroups(),
            ShowOpenLine = IsOpenLineVisible,
            ShowResolutionLine = IsResolutionLineVisible,
            ShowTotalCreatedLine = IsTotalCreatedLineVisible,
            ShowTotalClosedLine = IsTotalClosedLineVisible,
            ShowTargetLine = IsTargetLineVisible,
            ShowKpiCards = ShowKpiCards.IsChecked == true,
            ShowBarLabels = ShowBarLabels.IsChecked == true,
            ChartType = ChartTypeCombo.SelectedIndex,
            BarStyle = BarStyleCombo.SelectedIndex,
            ChartTheme = ChartThemeCombo.SelectedIndex,
            ShowGroupPanel = IsGroupPanelVisible,
            ShowAutoFilter = IsAutoFilterVisible,
            ShowTotalSummary = IsTotalSummaryVisible,
            AlternateRows = IsAlternateRows,
            ShowSearchPanel = ShowSearchPanel.IsChecked == true,
            ShowFilterPanel = ShowFilterPanel.IsChecked == true,
            GridStyle = GridStyleCombo.SelectedIndex,
        };
    }

    public (DateTime Start, DateTime End) GetDateRange()
    {
        var today = DateTime.Today;
        var dow = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var weekStart = today.AddDays(-dow);

        return RangeCombo.SelectedIndex switch
        {
            0 => (weekStart, weekStart.AddDays(7)),
            1 => (weekStart.AddDays(-7), weekStart.AddDays(7)),
            2 => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1)),
            3 => (new DateTime(today.Year, today.Month, 1).AddMonths(-2), new DateTime(today.Year, today.Month, 1).AddMonths(1)),
            4 => (new DateTime(today.Year, today.Month, 1).AddMonths(-5), new DateTime(today.Year, today.Month, 1).AddMonths(1)),
            5 => (new DateTime(today.Year, 1, 1), new DateTime(today.Year + 1, 1, 1)),
            _ => (weekStart, weekStart.AddDays(7))
        };
    }

    public string Bucket => ScaleCombo.SelectedIndex switch
    {
        0 => "day", 1 => "week", 2 => "month", _ => "day"
    };

    public bool IsOpenLineVisible => ShowOpenLine.IsChecked == true;
    public bool IsResolutionLineVisible => ShowResolutionLine.IsChecked == true;
    public bool IsTotalCreatedLineVisible => ShowTotalCreatedLine.IsChecked == true;
    public bool IsTotalClosedLineVisible => ShowTotalClosedLine.IsChecked == true;
    public bool IsTargetLineVisible => ShowTargetLine.IsChecked == true;

    public List<string>? GetSelectedTechs()
    {
        var all = TechCheckPanel.Children.OfType<WC.CheckBox>().ToList();
        if (all.Count == 0) return null;
        var selected = all.Where(cb => cb.IsChecked == true).Select(cb => cb.Content as string ?? "").ToList();
        if (selected.Count == all.Count) return null;
        if (selected.Count == 0) return new List<string> { "__NONE__" };
        return selected;
    }

    public List<string>? GetSelectedGroups()
    {
        var selected = new List<string>();
        int totalCount = 0;

        // Category checkboxes → expand to member groups
        foreach (var cb in GroupCategoryPanel.Children.OfType<WC.CheckBox>())
        {
            if (cb.Tag is SdGroupCategory cat)
            {
                totalCount++;
                if (cb.IsChecked == true)
                    selected.AddRange(cat.Members);
            }
        }

        // Uncategorized group checkboxes
        var uncategorized = GroupCheckPanel.Children.OfType<WC.CheckBox>().ToList();
        totalCount += uncategorized.Count;
        selected.AddRange(uncategorized.Where(cb => cb.IsChecked == true).Select(cb => cb.Content as string ?? ""));

        // If disabled groups exist, always return explicit list (never null)
        // so disabled groups are excluded from queries
        if (DisabledGroups.Count > 0)
        {
            if (selected.Count == 0) return new List<string> { "__NONE__" };
            return selected;
        }
        // No disabled groups — null means no filter
        if (selected.Count >= _allGroupNames.Count) return null;
        if (selected.Count == 0) return new List<string> { "__NONE__" };
        return selected;
    }

    // ── Grid display state ────────────────────────────────────────────────

    public bool IsGroupPanelVisible => ShowGroupPanel.IsChecked == true;
    public bool IsAutoFilterVisible => ShowAutoFilter.IsChecked == true;
    public bool IsTotalSummaryVisible => ShowTotalSummary.IsChecked == true;
    public bool IsAlternateRows => AlternateRows.IsChecked == true;

    // ── Data loading (called from MainWindow) ─────────────────────────────

    public void LoadTechnicians(List<string> names, List<SdTeam> teams)
    {
        _suppressEvents = true;

        // Team buttons
        TechTeamsPanel.Children.Clear();
        var teamRow = new WC.WrapPanel();
        foreach (var team in teams)
        {
            var members = team.Members;
            var btn = new WC.Button
            {
                Content = team.Name, Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 4), FontSize = 10
            };
            btn.Click += (_, _) =>
            {
                SetAllTechs(false);
                foreach (var cb in TechCheckPanel.Children.OfType<WC.CheckBox>())
                    if (members.Contains(cb.Content as string ?? "")) cb.IsChecked = true;
                FiltersChanged?.Invoke();
            };
            teamRow.Children.Add(btn);
        }
        TechTeamsPanel.Children.Add(teamRow);

        // Tech checkboxes
        TechCheckPanel.Children.Clear();
        foreach (var name in names)
        {
            var cb = new WC.CheckBox
            {
                Content = name, IsChecked = true,
                Foreground = new SolidColorBrush(ParseColor("#C0C0C0")),
                Margin = new Thickness(0, 0, 0, 2), FontSize = 11
            };
            cb.Checked += (_, _) => { if (!_suppressEvents) FiltersChanged?.Invoke(); };
            cb.Unchecked += (_, _) => { if (!_suppressEvents) FiltersChanged?.Invoke(); };
            TechCheckPanel.Children.Add(cb);
        }

        _suppressEvents = false;
    }

    private List<SdGroupCategory> _groupCategories = new();
    private List<string> _allGroupNames = new();

    /// <summary>Set of group names that are disabled in the tree (hidden from filter).</summary>
    public HashSet<string> DisabledGroups { get; set; } = new();

    public void LoadGroups(List<string> groups, List<SdGroupCategory> categories)
    {
        _suppressEvents = true;
        _groupCategories = categories;
        // Filter out disabled groups
        _allGroupNames = groups.Where(g => !DisabledGroups.Contains(g)).ToList();

        GroupCategoryPanel.Children.Clear();
        GroupCheckPanel.Children.Clear();

        // Track which groups are in an active category
        var categorized = new HashSet<string>();
        foreach (var cat in categories.Where(c => c.IsActive))
        {
            // Only include active members
            var activeMembers = cat.Members.Where(m => !DisabledGroups.Contains(m)).ToList();
            foreach (var m in activeMembers) categorized.Add(m);

            // Update the category's active member list for filter resolution
            cat.Members = activeMembers;

            // Show all active categories (even empty ones so user knows they exist)
            var memberList = activeMembers.Count > 0 ? string.Join(", ", activeMembers) : "(no groups assigned)";
            var catCb = new WC.CheckBox
            {
                Content = $"{cat.Name} ({activeMembers.Count})", IsChecked = activeMembers.Count > 0, Tag = cat,
                ToolTip = memberList,
                Foreground = new SolidColorBrush(ParseColor("#E0C080")),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2), FontSize = 11
            };
            catCb.Checked += (_, _) => { if (!_suppressEvents) FiltersChanged?.Invoke(); };
            catCb.Unchecked += (_, _) => { if (!_suppressEvents) FiltersChanged?.Invoke(); };
            GroupCategoryPanel.Children.Add(catCb);
        }

        // Uncategorized groups (excluding disabled)
        foreach (var g in _allGroupNames.Where(g => !categorized.Contains(g)))
        {
            var cb = new WC.CheckBox
            {
                Content = g, IsChecked = true,
                Foreground = new SolidColorBrush(ParseColor("#C0C0C0")),
                Margin = new Thickness(0, 0, 0, 2), FontSize = 11
            };
            cb.Checked += (_, _) => { if (!_suppressEvents) FiltersChanged?.Invoke(); };
            cb.Unchecked += (_, _) => { if (!_suppressEvents) FiltersChanged?.Invoke(); };
            GroupCheckPanel.Children.Add(cb);
        }
        _suppressEvents = false;
    }

    // Keep old signature for backward compat
    public void LoadGroups(List<string> groups) => LoadGroups(groups, new List<SdGroupCategory>());

    // ── Event handlers ────────────────────────────────────────────────────

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) FiltersChanged?.Invoke();
    }

    private void OnGridOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) GridOptionsChanged?.Invoke();
    }

    private void TechSelectAll_Click(object sender, RoutedEventArgs e) { SetAllTechs(true); FiltersChanged?.Invoke(); }
    private void TechSelectNone_Click(object sender, RoutedEventArgs e) { SetAllTechs(false); FiltersChanged?.Invoke(); }
    private void GroupSelectAll_Click(object sender, RoutedEventArgs e) { SetAllGroups(true); FiltersChanged?.Invoke(); }
    private void GroupSelectNone_Click(object sender, RoutedEventArgs e) { SetAllGroups(false); FiltersChanged?.Invoke(); }

    private void Apply_Click(object sender, RoutedEventArgs e) => FiltersChanged?.Invoke();
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _suppressEvents = true;
        RangeCombo.SelectedIndex = 0;
        ScaleCombo.SelectedIndex = 0;
        ShowOpenLine.IsChecked = true;
        ShowResolutionLine.IsChecked = true;
        ShowTotalCreatedLine.IsChecked = false;
        ShowTotalClosedLine.IsChecked = false;
        ShowTargetLine.IsChecked = true;
        ShowKpiCards.IsChecked = true;
        ShowBarLabels.IsChecked = false;
        ChartTypeCombo.SelectedIndex = 0;
        BarStyleCombo.SelectedIndex = 0;
        ChartThemeCombo.SelectedIndex = 0;
        ShowGroupPanel.IsChecked = true;
        ShowAutoFilter.IsChecked = true;
        ShowTotalSummary.IsChecked = true;
        AlternateRows.IsChecked = true;
        ShowSearchPanel.IsChecked = true;
        ShowFilterPanel.IsChecked = true;
        GridStyleCombo.SelectedIndex = 0;
        SetAllTechs(true);
        SetAllGroups(true);
        _suppressEvents = false;
        FiltersChanged?.Invoke();
        GridOptionsChanged?.Invoke();
    }

    // ── Grid layout actions — raised to MainWindow ──

    public event Action? ColumnChooserRequested;
    public event Action? BestFitRequested;
    public event Action? SaveLayoutRequested;
    public event Action? RestoreLayoutRequested;
    public event Action? ClearSortRequested;
    public event Action? ClearFilterRequested;
    public event Action? ClearGroupingRequested;

    private void ColumnChooser_Click(object sender, RoutedEventArgs e) => ColumnChooserRequested?.Invoke();
    private void BestFit_Click(object sender, RoutedEventArgs e) => BestFitRequested?.Invoke();
    private void SaveLayout_Click(object sender, RoutedEventArgs e) => SaveLayoutRequested?.Invoke();
    private void RestoreLayout_Click(object sender, RoutedEventArgs e) => RestoreLayoutRequested?.Invoke();
    private void ClearSort_Click(object sender, RoutedEventArgs e) => ClearSortRequested?.Invoke();
    private void ClearFilter_Click(object sender, RoutedEventArgs e) => ClearFilterRequested?.Invoke();
    private void ClearGrouping_Click(object sender, RoutedEventArgs e) => ClearGroupingRequested?.Invoke();

    private void SetAllTechs(bool val)
    {
        _suppressEvents = true;
        foreach (var cb in TechCheckPanel.Children.OfType<WC.CheckBox>()) cb.IsChecked = val;
        _suppressEvents = false;
    }

    private void SetAllGroups(bool val)
    {
        _suppressEvents = true;
        foreach (var cb in GroupCheckPanel.Children.OfType<WC.CheckBox>()) cb.IsChecked = val;
        _suppressEvents = false;
    }

    private static System.Windows.Media.Color ParseColor(string hex)
        => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
}
