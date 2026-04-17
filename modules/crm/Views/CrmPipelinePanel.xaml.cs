using System.Windows;
using System.Windows.Controls;
using WC = System.Windows.Controls;
using WMedia = System.Windows.Media;

namespace Central.Module.CRM.Views;

public partial class CrmPipelinePanel : System.Windows.Controls.UserControl
{
    private CrmDataService? _data;

    public CrmPipelinePanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void SetDsn(string dsn) => _data = new CrmDataService(dsn);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_data == null) return;
        try
        {
            var stages = await _data.LoadDealStagesAsync();
            var deals = await _data.LoadDealsAsync();
            var openDeals = deals.Where(d => d.IsOpen).ToList();

            PipelineColumns.Items.Clear();
            decimal totalPipeline = 0;
            decimal totalWeighted = 0;

            foreach (var stage in stages.Where(s => !s.IsWon && !s.IsLost))
            {
                var stageDeals = openDeals.Where(d => d.Stage == stage.Name).OrderByDescending(d => d.Value ?? 0).ToList();
                var stageValue = stageDeals.Sum(d => d.Value ?? 0);
                totalPipeline += stageValue;
                totalWeighted += stageDeals.Sum(d => d.WeightedValue);

                var col = BuildColumn(stage.Name, stage.Color, stageDeals);
                PipelineColumns.Items.Add(col);
            }

            TotalsText.Text = $"Total £{totalPipeline:N0} — Weighted £{totalWeighted:N0}";
        }
        catch (Exception ex)
        {
            Central.Engine.Services.NotificationService.Instance?.Error($"Pipeline load failed: {ex.Message}");
        }
    }

    private static WC.Border BuildColumn(string stageName, string color, List<Central.Engine.Models.CrmDeal> deals)
    {
        var stack = new WC.StackPanel { Margin = new Thickness(4) };

        // Header
        var header = new WC.Border
        {
            Background = ParseColor(color),
            CornerRadius = new CornerRadius(4, 4, 0, 0),
            Padding = new Thickness(8, 6, 8, 6)
        };
        var headerPanel = new WC.StackPanel();
        headerPanel.Children.Add(new WC.TextBlock
        {
            Text = stageName,
            Foreground = WMedia.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12
        });
        headerPanel.Children.Add(new WC.TextBlock
        {
            Text = $"{deals.Count} deals — £{deals.Sum(d => d.Value ?? 0):N0}",
            Foreground = WMedia.Brushes.White,
            FontSize = 11,
            Opacity = 0.8
        });
        header.Child = headerPanel;
        stack.Children.Add(header);

        // Cards
        var scroll = new WC.ScrollViewer
        {
            VerticalScrollBarVisibility = WC.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = WC.ScrollBarVisibility.Disabled,
            Background = Parse("#1E1E1E"),
            MaxHeight = 600,
            Width = 280
        };
        var cardsPanel = new WC.StackPanel { Margin = new Thickness(4) };
        foreach (var deal in deals.Take(50))
            cardsPanel.Children.Add(BuildDealCard(deal));
        scroll.Content = cardsPanel;
        stack.Children.Add(scroll);

        return new WC.Border
        {
            Child = stack,
            BorderBrush = Parse("#333"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Width = 290,
            Margin = new Thickness(4)
        };
    }

    private static WC.Border BuildDealCard(Central.Engine.Models.CrmDeal deal)
    {
        var panel = new WC.StackPanel { Margin = new Thickness(8, 6, 8, 6) };
        panel.Children.Add(new WC.TextBlock
        {
            Text = deal.Title,
            FontWeight = FontWeights.SemiBold,
            Foreground = Parse("#E0E0E0"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new WC.TextBlock
        {
            Text = deal.AccountName,
            Foreground = Parse("#909090"),
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0)
        });

        var valueRow = new WC.StackPanel { Orientation = WC.Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        valueRow.Children.Add(new WC.TextBlock
        {
            Text = $"{deal.Currency} {deal.Value ?? 0:N0}",
            FontWeight = FontWeights.SemiBold,
            Foreground = Parse("#22C55E"),
            FontSize = 13
        });
        valueRow.Children.Add(new WC.TextBlock
        {
            Text = $"  {deal.Probability}%",
            Foreground = Parse("#F59E0B"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(6, 0, 0, 0)
        });
        panel.Children.Add(valueRow);

        if (deal.ExpectedClose.HasValue)
        {
            panel.Children.Add(new WC.TextBlock
            {
                Text = $"Close: {deal.ExpectedClose:dd MMM yyyy}",
                Foreground = Parse("#707070"),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        if (!string.IsNullOrEmpty(deal.OwnerName))
        {
            panel.Children.Add(new WC.TextBlock
            {
                Text = deal.OwnerName,
                Foreground = Parse("#5B8AF5"),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        return new WC.Border
        {
            Child = panel,
            Background = Parse("#2B2B2B"),
            BorderBrush = Parse("#404040"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 4)
        };
    }

    private static WMedia.SolidColorBrush Parse(string hex) => ParseColor(hex);
    private static WMedia.SolidColorBrush ParseColor(string hex)
        => new((WMedia.Color)WMedia.ColorConverter.ConvertFromString(hex)!);
}
