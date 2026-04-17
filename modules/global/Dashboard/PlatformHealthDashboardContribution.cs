using System.Windows;
using System.Windows.Controls;
using Central.Engine.Widgets;
using Npgsql;
using WMedia = System.Windows.Media;

namespace Central.Module.Global.Dashboard;

/// <summary>
/// Always-first section: platform health. Renders DB connectivity and any
/// other platform-wide indicators that don't belong to a specific feature
/// module. Lives in Global (always loaded), so this section is always on
/// the dashboard.
/// </summary>
public class PlatformHealthDashboardContribution : IDashboardContribution
{
    public string SectionTitle => "Platform Health";
    public int SortOrder => 0;      // ensures it's always first
    public string? RequiredPermission => null;

    public async Task<IEnumerable<UIElement>> BuildCardsAsync(string dsn, CancellationToken ct = default)
    {
        bool dbOnline = false;
        try
        {
            await using var conn = new NpgsqlConnection(dsn);
            await conn.OpenAsync(ct);
            dbOnline = conn.State == System.Data.ConnectionState.Open;
        }
        catch { /* dbOnline stays false */ }

        return new UIElement[] { BuildStatusCard("Database", dbOnline) };
    }

    private static Border BuildStatusCard(string label, bool ok)
    {
        var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical, Margin = new Thickness(12, 8, 12, 8), MinWidth = 120 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = new WMedia.SolidColorBrush((WMedia.Color)WMedia.ColorConverter.ConvertFromString("#909090")!)
        });
        panel.Children.Add(new TextBlock
        {
            Text = ok ? "Online" : "Offline",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new WMedia.SolidColorBrush((WMedia.Color)WMedia.ColorConverter.ConvertFromString(ok ? "#22C55E" : "#EF4444")!)
        });
        return new Border
        {
            Child = panel,
            BorderBrush = new WMedia.SolidColorBrush((WMedia.Color)WMedia.ColorConverter.ConvertFromString("#333")!),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(3)
        };
    }
}
