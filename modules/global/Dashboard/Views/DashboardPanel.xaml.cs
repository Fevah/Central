using System.Windows;
using System.Windows.Controls;
using Central.Engine.Auth;
using Central.Engine.Models;
using Central.Engine.Services;
using Central.Engine.Shell;
using Central.Engine.Widgets;

namespace Central.Module.Global.Dashboard;

/// <summary>
/// Platform landing dashboard. A shell that renders whatever feature modules
/// have registered in <see cref="DashboardContributionRegistry"/>. No knowledge
/// of any specific module — disable Networking, its section disappears.
/// </summary>
public partial class DashboardPanel : System.Windows.Controls.UserControl
{
    private string? _dsn;

    public DashboardPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        PanelMessageBus.Subscribe<RefreshPanelMessage>(msg =>
        {
            if (msg.TargetPanel is "DashboardPanel" or "*")
                Dispatcher.InvokeAsync(() => _ = LoadAsync());
        });
    }

    public void SetDsn(string dsn) => _dsn = dsn;

    private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadAsync();

    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_dsn)) return;

        try
        {
            await RenderSectionsAsync();
            LoadActivity();
        }
        catch (Exception ex)
        {
            NotificationService.Instance?.Error($"Dashboard load failed: {ex.Message}");
        }
    }

    private async Task RenderSectionsAsync()
    {
        Sections.Children.Clear();

        foreach (var contrib in DashboardContributionRegistry.All)
        {
            // Permission gate — silently skip sections the user can't see.
            if (!string.IsNullOrEmpty(contrib.RequiredPermission)
                && !HasPermission(contrib.RequiredPermission))
                continue;

            IEnumerable<UIElement> cards;
            try
            {
                cards = await contrib.BuildCardsAsync(_dsn!);
            }
            catch (Exception ex)
            {
                // One broken contribution shouldn't blank the whole dashboard.
                NotificationService.Instance?.Warning(
                    $"Dashboard section '{contrib.SectionTitle}' failed: {ex.Message}");
                continue;
            }

            var cardList = cards.ToList();
            if (cardList.Count == 0) continue;   // nothing to show — skip section entirely

            // Section header
            Sections.Children.Add(new TextBlock
            {
                Text = contrib.SectionTitle,
                Style = (Style)FindResource("SectionHeader")
            });

            // Card container
            var wrap = new WrapPanel { Style = (Style)FindResource("CardPanel") };
            foreach (var card in cardList) wrap.Children.Add(card);
            Sections.Children.Add(wrap);
        }
    }

    private static bool HasPermission(string code)
        => AuthContext.Instance?.HasPermission(code) ?? false;

    private void LoadActivity()
    {
        var recent = NotificationService.Instance?.Recent;
        if (recent == null) return;

        ActivityList.ItemsSource = recent.Take(20).Select(n => new
        {
            Timestamp = n.Timestamp,
            Icon = n.Type switch
            {
                NotificationType.Success => "\u2713",
                NotificationType.Warning => "\u26A0",
                NotificationType.Error   => "\u2717",
                _                        => "\u2022"
            },
            Message = n.Message
        }).ToList();
    }
}
