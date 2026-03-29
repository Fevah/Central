using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Central.Core.Models;

public class InterfaceOptics : INotifyPropertyChanged
{
    public int Id { get; set; }
    public Guid SwitchId { get; set; }
    public DateTime CapturedAt { get; set; }
    public string InterfaceName { get; set; } = "";
    public string Channel { get; set; } = "";       // "" or "C1","C2","C3","C4"
    public decimal? TempC { get; set; }
    public decimal? TempF { get; set; }
    public decimal? Voltage { get; set; }
    public decimal? BiasMa { get; set; }
    public decimal? TxPowerDbm { get; set; }
    public decimal? RxPowerDbm { get; set; }
    public string ModuleType { get; set; } = "";

    // Display helpers
    public string DisplayTx => TxPowerDbm.HasValue ? $"{TxPowerDbm:F2} dBm" : "";
    public string DisplayRx => RxPowerDbm.HasValue ? $"{RxPowerDbm:F2} dBm" : "";
    public string DisplayTemp => TempC.HasValue ? $"{TempC:F1}°C" : "";

    /// <summary>
    /// RX power status color — green if ok, yellow if marginal, red if low/no light
    /// </summary>
    public string RxColor =>
        !RxPowerDbm.HasValue ? "#6B7280" :    // grey — no data
        RxPowerDbm <= -30 ? "#EF4444" :        // red — no light
        RxPowerDbm <= -20 ? "#F59E0B" :        // yellow — marginal
        "#22C55E";                              // green — ok

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    /// <summary>
    /// Parse "run show interface diagnostics optics all" output.
    /// Multi-line format: first line has interface + all columns, continuation lines have channel data only.
    /// </summary>
    public static List<InterfaceOptics> Parse(Guid switchId, string output)
    {
        var list = new List<InterfaceOptics>();
        if (string.IsNullOrWhiteSpace(output)) return list;

        var lines = output.Split('\n');
        var headerFound = false;
        string currentIface = "";
        string currentModuleType = "";
        decimal? currentTempC = null, currentTempF = null, currentVoltage = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Detect header
            if (!headerFound)
            {
                if (line.Contains("Interface") && line.Contains("Tx Power"))
                    headerFound = true;
                continue;
            }

            // Skip separator
            if (line.TrimStart().StartsWith("---")) continue;
            // Skip prompt
            if (line.Contains("#") && line.Contains("@")) continue;

            // Determine if this is a main line (starts with interface name) or continuation
            var trimmed = line.TrimStart();
            var isMainLine = Regex.IsMatch(trimmed, @"^[a-z]{2,3}-\d");

            if (isMainLine)
            {
                // Main line: xe-1/1/4  26.00/78.80  3.42  5.78 [C1]  -0.21 [C1]  -20.00 [C1]  100G_BASE_AOC
                var parts = Regex.Split(trimmed, @"\s{2,}");
                if (parts.Length < 2) continue;

                currentIface = parts[0].Trim();

                // Parse temp (format: "26.00/78.80")
                currentTempC = null; currentTempF = null;
                if (parts.Length >= 2)
                {
                    var tempMatch = Regex.Match(parts[1], @"(-?[\d.]+)/(-?[\d.]+)");
                    if (tempMatch.Success)
                    {
                        currentTempC = ParseDecimal(tempMatch.Groups[1].Value);
                        currentTempF = ParseDecimal(tempMatch.Groups[2].Value);
                    }
                }

                // Voltage
                currentVoltage = parts.Length >= 3 ? ParseDecimal(parts[2].Trim()) : null;

                // Module type (last column)
                currentModuleType = "";
                if (parts.Length >= 7) currentModuleType = parts[6].Trim();
                else if (parts.Length >= 6 && !parts[^1].Contains("[")) currentModuleType = parts[^1].Trim();

                // Parse bias/tx/rx with channel
                var (bias, channel1) = ParseValueWithChannel(parts.Length >= 4 ? parts[3] : "");
                var (tx, _) = ParseValueWithChannel(parts.Length >= 5 ? parts[4] : "");
                var (rx, _) = ParseValueWithChannel(parts.Length >= 6 ? parts[5] : "");

                list.Add(new InterfaceOptics
                {
                    SwitchId = switchId,
                    CapturedAt = DateTime.UtcNow,
                    InterfaceName = currentIface,
                    Channel = channel1,
                    TempC = currentTempC,
                    TempF = currentTempF,
                    Voltage = currentVoltage,
                    BiasMa = bias,
                    TxPowerDbm = tx,
                    RxPowerDbm = rx,
                    ModuleType = currentModuleType,
                });
            }
            else
            {
                // Continuation line: just bias [Cx]  tx [Cx]  rx [Cx]  (possibly module type)
                var parts = Regex.Split(trimmed, @"\s{2,}");
                if (parts.Length < 1 || string.IsNullOrEmpty(currentIface)) continue;

                var (bias, channel) = ParseValueWithChannel(parts.Length >= 1 ? parts[0] : "");
                var (tx, _) = ParseValueWithChannel(parts.Length >= 2 ? parts[1] : "");
                var (rx, _) = ParseValueWithChannel(parts.Length >= 3 ? parts[2] : "");

                if (string.IsNullOrEmpty(channel)) continue; // Skip if no channel marker

                list.Add(new InterfaceOptics
                {
                    SwitchId = switchId,
                    CapturedAt = DateTime.UtcNow,
                    InterfaceName = currentIface,
                    Channel = channel,
                    TempC = currentTempC,
                    TempF = currentTempF,
                    Voltage = currentVoltage,
                    BiasMa = bias,
                    TxPowerDbm = tx,
                    RxPowerDbm = rx,
                    ModuleType = currentModuleType,
                });
            }
        }
        return list;
    }

    /// <summary>Parse "5.78 [C1]" → (5.78, "C1") or "-0.21" → (-0.21, "")</summary>
    private static (decimal? Value, string Channel) ParseValueWithChannel(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return (null, "");
        input = input.Trim();

        var channelMatch = Regex.Match(input, @"\[([A-Za-z]\d+)\]");
        var channel = channelMatch.Success ? channelMatch.Groups[1].Value : "";

        var numStr = Regex.Replace(input, @"\s*\[.*?\]\s*", "").Trim();
        var value = ParseDecimal(numStr);

        return (value, channel);
    }

    private static decimal? ParseDecimal(string s)
    {
        s = s?.Trim() ?? "";
        if (string.IsNullOrEmpty(s) || s == "-" || s == "N/A") return null;
        return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
