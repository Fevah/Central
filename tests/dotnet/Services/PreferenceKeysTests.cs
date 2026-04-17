using Central.Engine.Services;

namespace Central.Tests.Services;

/// <summary>Tests that PreferenceKeys constants are correctly defined and unique.</summary>
public class PreferenceKeysTests
{
    [Fact]
    public void HideReserved_HasCorrectValue()
    {
        Assert.Equal("pref.hide_reserved", PreferenceKeys.HideReserved);
    }

    [Fact]
    public void Theme_HasCorrectValue()
    {
        Assert.Equal("pref.theme", PreferenceKeys.Theme);
    }

    [Fact]
    public void DockLayout_HasCorrectValue()
    {
        Assert.Equal("layout.dock", PreferenceKeys.DockLayout);
    }

    [Fact]
    public void AllKeys_Unique()
    {
        var fields = typeof(PreferenceKeys).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var values = fields.Select(f => f.GetValue(null) as string).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void AllKeys_NonEmpty()
    {
        var fields = typeof(PreferenceKeys).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        foreach (var f in fields)
        {
            var val = f.GetValue(null) as string;
            Assert.False(string.IsNullOrWhiteSpace(val), $"{f.Name} should not be empty");
        }
    }

    [Fact]
    public void PrefKeys_StartWithPref()
    {
        Assert.StartsWith("pref.", PreferenceKeys.HideReserved);
        Assert.StartsWith("pref.", PreferenceKeys.SiteSelections);
        Assert.StartsWith("pref.", PreferenceKeys.DevicesSearch);
        Assert.StartsWith("pref.", PreferenceKeys.ActiveRibbonTab);
        Assert.StartsWith("pref.", PreferenceKeys.ActiveDocTab);
        Assert.StartsWith("pref.", PreferenceKeys.GridFilters);
        Assert.StartsWith("pref.", PreferenceKeys.ScanEnabled);
        Assert.StartsWith("pref.", PreferenceKeys.ScanInterval);
        Assert.StartsWith("pref.", PreferenceKeys.Theme);
    }

    [Fact]
    public void LayoutKeys_StartWithLayout()
    {
        Assert.StartsWith("layout.", PreferenceKeys.PanelStates);
        Assert.StartsWith("layout.", PreferenceKeys.DockLayout);
        Assert.StartsWith("layout.", PreferenceKeys.DevicesGrid);
        Assert.StartsWith("layout.", PreferenceKeys.SwitchGrid);
        Assert.StartsWith("layout.", PreferenceKeys.AdminGrid);
        Assert.StartsWith("layout.", PreferenceKeys.UsersGrid);
        Assert.StartsWith("layout.", PreferenceKeys.RolesGrid);
        Assert.StartsWith("layout.", PreferenceKeys.DetailTabOrder);
    }
}
