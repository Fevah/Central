using System.Windows;
using System.Windows.Media;
using DevExpress.Xpf.Diagram;
using Central.Engine.Models;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;

namespace Central.Module.Routing.Views;

public partial class DiagramPanel : System.Windows.Controls.UserControl
{
    public DiagramPanel() => InitializeComponent();

    public DiagramControl Diagram => NetworkDiagram;
    public string StatusText { get => DiagramStatusText.Text; set => DiagramStatusText.Text = value; }

    public event Func<Task>? RefreshRequested;
    public event Action? FitRequested;
    public event Action? TreeLayoutRequested;
    public event Action? SugiyamaLayoutRequested;

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    { if (RefreshRequested != null) await RefreshRequested.Invoke(); }
    private void FitButton_Click(object sender, RoutedEventArgs e) => FitRequested?.Invoke();
    private void TreeLayoutButton_Click(object sender, RoutedEventArgs e) => TreeLayoutRequested?.Invoke();
    private void SugiyamaButton_Click(object sender, RoutedEventArgs e) => SugiyamaLayoutRequested?.Invoke();

    // ── Diagram builder (moved from MainWindow.xaml.cs) ──

    private const double NODE_W = 200, NODE_H = 80, TIER_GAP = 140;
    private const double NODE_GAP_X = 40, BLDG_GAP = 120, BLDG_PAD = 20;
    private static readonly string[] TierOrder = { "core", "firewall", "l1", "management", "storage", "l2", "leaf", "other" };

    /// <summary>Build the network diagram from device + link data.</summary>
    public async Task BuildDiagramAsync(
        IEnumerable<DeviceRecord> allDevices,
        List<string> selectedSites,
        Func<List<string>, bool, Task<List<P2PLink>>> getP2P,
        Func<List<string>, bool, Task<List<B2BLink>>> getB2B)
    {
        try
        {
            StatusText = "Loading…";
            Diagram.Items.Clear();
            if (selectedSites.Count == 0) { StatusText = "No sites selected"; return; }

            var devices = allDevices
                .Where(d => d.Status == "Active" && selectedSites.Contains(d.Building)).ToList();
            var p2p = await getP2P(selectedSites, true);
            var b2b = await getB2B(selectedSites, true);

            var nodes = new Dictionary<string, DiagramShape>(StringComparer.OrdinalIgnoreCase);
            var buildings = devices.GroupBy(d => d.Building).OrderBy(g => g.Key).ToList();
            double bldgX = 50;
            var bldgBounds = new Dictionary<string, (double x, double y, double w, double h)>(StringComparer.OrdinalIgnoreCase);

            foreach (var bldg in buildings)
            {
                var tiers = new Dictionary<string, List<DeviceRecord>>();
                foreach (var dev in bldg)
                {
                    var tier = ClassifyTier(dev.DeviceType);
                    if (!tiers.ContainsKey(tier)) tiers[tier] = new();
                    tiers[tier].Add(dev);
                }

                double colMaxW = 0, y = 80;
                foreach (var tierName in TierOrder)
                {
                    if (!tiers.TryGetValue(tierName, out var tierDevs)) continue;
                    var sorted = tierDevs.OrderBy(d => d.SwitchName).ToList();
                    double rowW = sorted.Count * (NODE_W + NODE_GAP_X) - NODE_GAP_X;
                    double startX = bldgX + BLDG_PAD;

                    for (int i = 0; i < sorted.Count; i++)
                    {
                        var dev = sorted[i];
                        if (string.IsNullOrEmpty(dev.SwitchName)) continue;
                        var (bg, badge) = GetDeviceStyle(dev.DeviceType);
                        var lines = new List<string> { $"{badge}  {dev.SwitchName}" };
                        if (!string.IsNullOrEmpty(dev.Ip)) lines.Add($"IP  {dev.Ip}");
                        if (!string.IsNullOrEmpty(dev.LoopbackIp)) lines.Add($"Lo  {dev.LoopbackIp}");
                        if (!string.IsNullOrEmpty(dev.ManagementIp)) lines.Add($"Mg  {dev.ManagementIp}");
                        if (!string.IsNullOrEmpty(dev.Asn)) lines.Add($"ASN {dev.Asn}");

                        double h = Math.Max(NODE_H, 24 + lines.Count * 14);
                        var shape = CreateDeviceShape(string.Join("\n", lines), NODE_W, h, bg);
                        shape.Position = new Point(startX + i * (NODE_W + NODE_GAP_X), y);
                        shape.Tag = dev.SwitchName;
                        nodes[dev.SwitchName] = shape;
                        Diagram.Items.Add(shape);
                    }
                    if (rowW > colMaxW) colMaxW = rowW;
                    double maxH = sorted.Max(d => Math.Max(NODE_H, 24 + 5 * 14));
                    y += maxH + TIER_GAP;
                }

                double totalW = Math.Max(colMaxW + BLDG_PAD * 2, NODE_W + BLDG_PAD * 2);
                double totalH = y + BLDG_PAD;
                var bldgBox = new DiagramShape
                {
                    Content = $"  {bldg.Key}", Position = new Point(bldgX, 0),
                    Width = totalW, Height = totalH,
                    Background = new SolidColorBrush(Color.FromArgb(0x18, 0x90, 0xCA, 0xF9)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x33, 0x55, 0x77)),
                    StrokeThickness = 1.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0xA0, 0xC0)),
                    FontSize = 14, FontWeight = FontWeights.Bold,
                    CanSelect = false, CanMove = false, CanResize = false,
                };
                Diagram.Items.Insert(0, bldgBox);
                bldgBounds[bldg.Key] = (bldgX, 0, totalW, totalH);
                bldgX += totalW + BLDG_GAP;
            }

            // P2P connectors
            int p2pCount = 0;
            foreach (var link in p2p)
            {
                if (!nodes.TryGetValue(link.DeviceA, out var a) || !nodes.TryGetValue(link.DeviceB, out var b)) continue;
                Diagram.Items.Add(new DiagramConnector
                {
                    BeginItem = a, EndItem = b,
                    Content = $"{link.DeviceAIp}  ⟷  {link.DeviceBIp}\nVL{link.Vlan} {link.Subnet}",
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 7.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0xB5, 0xF6)),
                    StrokeThickness = 1, Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x5F, 0x7C)),
                });
                p2pCount++;
            }

            // B2B connectors
            int b2bCount = 0;
            foreach (var link in b2b)
            {
                var aName = !string.IsNullOrEmpty(link.DeviceA) ? link.DeviceA : link.BuildingA;
                var bName = !string.IsNullOrEmpty(link.DeviceB) ? link.DeviceB : link.BuildingB;
                if (!nodes.ContainsKey(aName))
                {
                    var pos = bldgBounds.TryGetValue(link.BuildingA, out var ba)
                        ? new Point(ba.x + ba.w / 2 - 70, ba.h + 20) : new Point(0, 0);
                    var gw = CreateGatewayShape($"⬡ {link.BuildingA}\n{link.DeviceAIp}", pos);
                    nodes[aName] = gw; Diagram.Items.Add(gw);
                }
                if (!nodes.ContainsKey(bName))
                {
                    var pos = bldgBounds.TryGetValue(link.BuildingB, out var bb)
                        ? new Point(bb.x + bb.w / 2 - 70, bb.h + 20) : new Point(0, 0);
                    var gw = CreateGatewayShape($"⬡ {link.BuildingB}\n{link.DeviceBIp}", pos);
                    nodes[bName] = gw; Diagram.Items.Add(gw);
                }
                if (!nodes.TryGetValue(aName, out var nodeA) || !nodes.TryGetValue(bName, out var nodeB)) continue;
                Diagram.Items.Add(new DiagramConnector
                {
                    BeginItem = nodeA, EndItem = nodeB,
                    Content = $"{link.DeviceAIp}  ⟷  {link.DeviceBIp}\nB2B VL{link.Vlan} {link.Subnet}",
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 7.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)),
                    StrokeThickness = 2.5, Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                });
                b2bCount++;
            }

            if (Diagram.Items.Count > 0) Diagram.FitToItems(Diagram.Items.ToList());
            StatusText = $"{nodes.Count} devices · {p2pCount} P2P · {b2bCount} B2B · {buildings.Count} sites";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    private static string ClassifyTier(string? dt)
    {
        var t = (dt ?? "").ToLower();
        if (t.Contains("core") && !t.Contains("l1") && !t.Contains("l2")) return "core";
        if (t.Contains("firewall")) return "firewall";
        if (t.Contains("l1")) return "l1";
        if (t.Contains("management")) return "management";
        if (t.Contains("storage")) return "storage";
        if (t.Contains("l2")) return "l2";
        if (t.Contains("leaf")) return "leaf";
        return "other";
    }

    private static DiagramShape CreateDeviceShape(string content, double w, double h, SolidColorBrush bg)
        => new() { Content = content, Width = w, Height = h, Background = bg,
            Foreground = Brushes.White, FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10, StrokeThickness = 1, Stroke = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)) };

    private static DiagramShape CreateGatewayShape(string content, System.Windows.Point pos)
        => new() { Content = content, Position = pos, Width = 140, Height = 45,
            Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x27, 0x23)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x80)),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 10,
            StrokeThickness = 2, Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)) };

    private static (SolidColorBrush bg, string badge) GetDeviceStyle(string? deviceType)
    {
        var dt = (deviceType ?? "").ToLower();
        if (dt.Contains("core") && !dt.Contains("l1") && !dt.Contains("l2")) return (new(Color.FromRgb(0x1A, 0x23, 0x7E)), "▲ CORE");
        if (dt.Contains("l1")) return (new(Color.FromRgb(0x00, 0x69, 0x5C)), "◆ DIST");
        if (dt.Contains("l2")) return (new(Color.FromRgb(0x2E, 0x7D, 0x32)), "● ACCESS");
        if (dt.Contains("management")) return (new(Color.FromRgb(0x4A, 0x14, 0x8C)), "■ MGMT");
        if (dt.Contains("storage")) return (new(Color.FromRgb(0xBF, 0x36, 0x0C)), "◼ STOR");
        if (dt.Contains("leaf")) return (new(Color.FromRgb(0x0D, 0x47, 0xA1)), "○ LEAF");
        if (dt.Contains("firewall")) return (new(Color.FromRgb(0xB7, 0x1C, 0x1C)), "✦ FW");
        if (dt.Contains("reserved")) return (new(Color.FromRgb(0x61, 0x61, 0x61)), "▫ RES");
        return (new(Color.FromRgb(0x37, 0x47, 0x4F)), "◇");
    }
}
