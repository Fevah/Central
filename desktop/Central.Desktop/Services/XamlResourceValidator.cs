using System.Windows;
using System.Windows.Controls;

namespace Central.Desktop.Services;

/// <summary>
/// Engine-level startup validator — checks that module UserControls have required
/// local resources (converters, styles) that they can't inherit from the Window.
///
/// Call ValidateAll() during startup to catch missing resources BEFORE layout crashes.
/// Logs warnings instead of crashing.
/// </summary>
public static class XamlResourceValidator
{
    private static readonly string[] RequiredConverters = { "StringToBrush" };

    /// <summary>Validate that a panel's resources include all required converters.</summary>
    public static List<string> Validate(FrameworkElement panel, string panelName)
    {
        var missing = new List<string>();
        foreach (var key in RequiredConverters)
        {
            if (panel.TryFindResource(key) == null)
                missing.Add($"{panelName}: missing resource '{key}'");
        }
        return missing;
    }

    /// <summary>Validate all panels that use data-template bindings with converters.
    /// Call after InitializeComponent to catch issues before first layout pass.</summary>
    public static void ValidateAndWarn(params (FrameworkElement Panel, string Name)[] panels)
    {
        var allMissing = new List<string>();
        foreach (var (panel, name) in panels)
        {
            allMissing.AddRange(Validate(panel, name));
        }

        if (allMissing.Count > 0)
        {
            var msg = string.Join("\n", allMissing);
            System.Diagnostics.Debug.WriteLine($"XAML RESOURCE WARNINGS:\n{msg}");
            // Log to startup.log if available
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} XAML WARNINGS:\n{msg}\n");
            }
            catch { }
        }
    }
}
