using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Central.Core.Services;

namespace Central.Module.Dashboard.Views;

public partial class NotificationCenterPanel : System.Windows.Controls.UserControl
{
    private readonly ObservableCollection<NotificationRow> _allRows = new();
    private readonly ObservableCollection<NotificationRow> _filteredRows = new();

    public NotificationCenterPanel()
    {
        InitializeComponent();
        NotifGrid.ItemsSource = _filteredRows;

        FilterType.SelectionChanged += (_, _) => ApplyFilter();
        BtnClear.Click += (_, _) =>
        {
            _allRows.Clear();
            _filteredRows.Clear();
            NotificationService.Instance?.ClearRecent();
        };

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Load existing notifications
        var recent = NotificationService.Instance?.Recent;
        if (recent != null)
        {
            foreach (var n in recent)
                _allRows.Insert(0, ToRow(n));
        }
        ApplyFilter();

        // Subscribe to new notifications
        if (NotificationService.Instance != null)
        {
            NotificationService.Instance.NotificationReceived += n =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    var row = ToRow(n);
                    _allRows.Insert(0, row);

                    while (_allRows.Count > 500)
                        _allRows.RemoveAt(_allRows.Count - 1);

                    ApplyFilter();
                });
            };
        }
    }

    private void ApplyFilter()
    {
        var filterIdx = FilterType.SelectedIndex;
        _filteredRows.Clear();

        foreach (var row in _allRows)
        {
            if (filterIdx == 0 || row.TypeName == ((ComboBoxItem)FilterType.Items[filterIdx]).Content.ToString())
                _filteredRows.Add(row);
        }
    }

    private static NotificationRow ToRow(Notification n) => new()
    {
        Icon = n.Icon,
        Timestamp = n.Timestamp,
        TypeName = n.Type.ToString(),
        Title = n.Title,
        Message = n.Message,
        Source = n.Source ?? ""
    };

    public class NotificationRow : INotifyPropertyChanged
    {
        public string Icon { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string TypeName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Source { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
