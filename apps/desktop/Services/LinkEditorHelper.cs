using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Editors;
using Central.Persistence;
using Central.Engine.Models;

namespace Central.Desktop.Services;

/// <summary>
/// Shared helper for link grid editors (P2P, B2B, FW).
/// Eliminates duplicated ShownEditor/port dropdown logic across panels.
/// </summary>
public class LinkEditorHelper
{
    private readonly ObservableCollection<DeviceRecord> _devices;
    private readonly ObservableCollection<SwitchRecord> _switches;
    private readonly ObservableCollection<string> _buildingOptions;
    private readonly DbRepository _repo;
    private readonly Dictionary<Guid, List<SwitchInterface>> _ifaceCache = new();
    private DataTemplate? _portItemTemplate;

    private static readonly SwitchInterface NonePort = new() { InterfaceName = "", Description = "(None)" };

    public LinkEditorHelper(
        ObservableCollection<DeviceRecord> devices,
        ObservableCollection<SwitchRecord> switches,
        ObservableCollection<string> buildingOptions,
        DbRepository repo)
    {
        _devices = devices;
        _switches = switches;
        _buildingOptions = buildingOptions;
        _repo = repo;
    }

    // ── Device Dropdown ─────────────────────────────────────────────────

    /// <summary>Populate a device name dropdown, filtered by building (strips -L1/-L2 suffix).</summary>
    public void WireDeviceDropdown(ComboBoxEdit combo, string? building)
    {
        combo.ItemsSource = GetFilteredDeviceNames(building);
    }

    /// <summary>Populate a building dropdown.</summary>
    public void WireBuildingDropdown(ComboBoxEdit combo)
    {
        combo.ItemsSource = _buildingOptions;
    }

    /// <summary>Get device names filtered by building. Strips -L1/-L2 suffix to match base building.</summary>
    public List<string> GetFilteredDeviceNames(string? building)
    {
        var all = _devices.OrderBy(d => d.Building).ThenBy(d => d.SwitchName).ToList();
        if (!string.IsNullOrEmpty(building))
        {
            var baseBuilding = Regex.Replace(building, @"-L\d+$", "", RegexOptions.IgnoreCase);
            var filtered = all
                .Where(d => string.Equals(d.Building, building, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(d.Building, baseBuilding, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.SwitchName)
                .ToList();
            if (filtered.Count > 0) return filtered;
        }
        return all.Select(d => d.SwitchName).ToList();
    }

    // ── Port Dropdown ───────────────────────────────────────────────────

    /// <summary>Populate a port dropdown with multi-column interface data.</summary>
    public void WirePortDropdown(ComboBoxEdit combo, string? deviceName)
    {
        combo.ItemsSource = GetPortObjectsForDevice(deviceName);
        _ = LoadPortObjectsCacheAsync(deviceName, combo);

        combo.ItemTemplate = _portItemTemplate ??= BuildPortItemTemplate();
        combo.PopupWidth = 750;
        combo.PopupMaxHeight = 400;
        combo.NullText = "(None)";
        combo.AllowNullInput = true;
    }

    /// <summary>Synchronous — returns cached SwitchInterface list or generated fallback.</summary>
    public List<SwitchInterface> GetPortObjectsForDevice(string? deviceName)
    {
        var list = new List<SwitchInterface> { NonePort };

        if (string.IsNullOrEmpty(deviceName))
        {
            list.AddRange(GeneratePortList().Select(p => new SwitchInterface { InterfaceName = p }));
            return list;
        }

        var sw = _switches.FirstOrDefault(s =>
            string.Equals(s.Hostname, deviceName, StringComparison.OrdinalIgnoreCase));

        if (sw != null && sw.Id != Guid.Empty && _ifaceCache.TryGetValue(sw.Id, out var cached))
        {
            list.AddRange(cached
                .Where(i => i.InterfaceName.Contains("/") || i.InterfaceName.StartsWith("ae"))
                .OrderBy(i => i.InterfaceName, Comparer<string>.Create(CompareInterfaceNames)));
            return list;
        }

        var device = _devices.FirstOrDefault(d =>
            string.Equals(d.SwitchName, deviceName, StringComparison.OrdinalIgnoreCase));
        var dt = (device?.DeviceType ?? "").ToLower();
        var ports = (dt.Contains("l2") || dt.Contains("management")) ? GeneratePortListGe() : GeneratePortList();
        list.AddRange(ports.Select(p => new SwitchInterface { InterfaceName = p }));
        return list;
    }

    /// <summary>Async load interface data from DB and update the combo when ready.</summary>
    public async Task LoadPortObjectsCacheAsync(string? deviceName, ComboBoxEdit combo)
    {
        if (string.IsNullOrEmpty(deviceName)) return;

        var sw = _switches.FirstOrDefault(s =>
            string.Equals(s.Hostname, deviceName, StringComparison.OrdinalIgnoreCase));
        if (sw == null || sw.Id == Guid.Empty) return;
        if (_ifaceCache.ContainsKey(sw.Id)) return;

        try
        {
            var ifaces = await _repo.GetSwitchInterfacesAsync(sw.Id);
            if (ifaces.Count > 0)
            {
                var optics = await _repo.GetLatestOpticsAsync(sw.Id);
                SwitchInterface.MergeOptics(ifaces, optics);
                _ifaceCache[sw.Id] = ifaces;

                var filtered = new List<SwitchInterface> { NonePort };
                filtered.AddRange(ifaces
                    .Where(i => i.InterfaceName.Contains("/") || i.InterfaceName.StartsWith("ae"))
                    .OrderBy(i => i.InterfaceName, Comparer<string>.Create(CompareInterfaceNames)));

                if (combo.IsVisible)
                    combo.ItemsSource = filtered;
            }
            else if (!string.IsNullOrEmpty(sw.HardwareModel))
            {
                var modelPorts = await _repo.GetModelInterfacesAsync(sw.HardwareModel);
                if (modelPorts.Count > 0)
                {
                    var asObjects = new List<SwitchInterface> { NonePort };
                    asObjects.AddRange(modelPorts.Select(p => new SwitchInterface { InterfaceName = p }));
                    if (combo.IsVisible)
                        combo.ItemsSource = asObjects;
                }
            }
        }
        catch (Exception ex) { AppLogger.LogException("LinkEditor", ex, "LoadPortObjectsCacheAsync"); }
    }

    // ── Port List Generation ────────────────────────────────────────────

    public static List<string> GeneratePortList()
    {
        var ports = new List<string>();
        for (int i = 1; i <= 32; i++) ports.Add($"xe-1/1/{i}");
        for (int i = 1; i <= 4; i++) { ports.Add($"xe-1/1/31.{i}"); ports.Add($"xe-1/1/32.{i}"); }
        for (int i = 1; i <= 48; i++) ports.Add($"ge-1/1/{i}");
        for (int i = 1; i <= 4; i++) ports.Add($"te-1/1/{i}");
        return ports;
    }

    public static List<string> GeneratePortListGe()
    {
        var ports = new List<string>();
        for (int i = 1; i <= 48; i++) ports.Add($"ge-1/1/{i}");
        for (int i = 1; i <= 4; i++) ports.Add($"te-1/1/{i}");
        return ports;
    }

    /// <summary>Natural sort for interface names: xe-1/1/2 before xe-1/1/10.</summary>
    public static int CompareInterfaceNames(string a, string b)
    {
        var partsA = Regex.Split(a, @"(\d+)");
        var partsB = Regex.Split(b, @"(\d+)");
        for (int i = 0; i < Math.Min(partsA.Length, partsB.Length); i++)
        {
            if (int.TryParse(partsA[i], out var na) && int.TryParse(partsB[i], out var nb))
            {
                if (na != nb) return na.CompareTo(nb);
            }
            else
            {
                var cmp = string.Compare(partsA[i], partsB[i], StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
            }
        }
        return partsA.Length.CompareTo(partsB.Length);
    }

    // ── Port Item Template ──────────────────────────────────────────────

    private static DataTemplate BuildPortItemTemplate()
    {
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="95" />
                        <ColumnDefinition Width="60" />
                        <ColumnDefinition Width="50" />
                        <ColumnDefinition Width="55" />
                        <ColumnDefinition Width="170" />
                        <ColumnDefinition Width="120" />
                        <ColumnDefinition Width="90" />
                        <ColumnDefinition Width="80" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" FontFamily="Consolas" FontSize="11" Foreground="#E0E0E0" Margin="2,1">
                        <TextBlock.Style>
                            <Style TargetType="TextBlock">
                                <Setter Property="Text" Value="{Binding InterfaceName}" />
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding InterfaceName}" Value="">
                                        <Setter Property="Text" Value="(None)" />
                                        <Setter Property="Foreground" Value="#6B7280" />
                                        <Setter Property="FontStyle" Value="Italic" />
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </TextBlock.Style>
                    </TextBlock>
                    <TextBlock Grid.Column="1" Text="{Binding AdminStatus}"   FontSize="10" Foreground="#999" Margin="2,1" />
                    <TextBlock Grid.Column="2" Text="{Binding LinkStatus}" FontSize="10" FontWeight="SemiBold" Margin="2,1">
                        <TextBlock.Style>
                            <Style TargetType="TextBlock">
                                <Setter Property="Foreground" Value="#6B7280" />
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding LinkStatus}" Value="Up">
                                        <Setter Property="Foreground" Value="#22C55E" />
                                    </DataTrigger>
                                    <DataTrigger Binding="{Binding LinkStatus}" Value="Down">
                                        <Setter Property="Foreground" Value="#EF4444" />
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </TextBlock.Style>
                    </TextBlock>
                    <TextBlock Grid.Column="3" Text="{Binding Speed}"         FontSize="10" Foreground="#90CAF9" Margin="2,1" />
                    <TextBlock Grid.Column="4" Text="{Binding Description}"   FontSize="10" Foreground="#CCC" Margin="2,1" TextTrimming="CharacterEllipsis" />
                    <TextBlock Grid.Column="5" Text="{Binding LldpHost}"      FontSize="10" Foreground="#81C784" Margin="2,1" TextTrimming="CharacterEllipsis" />
                    <TextBlock Grid.Column="6" Text="{Binding LldpPort}"      FontSize="10" Foreground="#81C784" Margin="2,1" TextTrimming="CharacterEllipsis" />
                    <TextBlock Grid.Column="7" Text="{Binding ModuleType}"    FontSize="10" Foreground="#999" Margin="2,1" TextTrimming="CharacterEllipsis" />
                </Grid>
            </DataTemplate>
            """;
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }
}
