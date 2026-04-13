using Central.Core.Models;

namespace Central.Tests.Models;

/// <summary>Extended PanelCustomization model tests.</summary>
public class PanelCustomizationExtendedTests
{
    [Fact]
    public void GridSettings_SetLists_WorkCorrectly()
    {
        var gs = new GridSettings
        {
            ColumnOrder = new List<string> { "Name", "Status", "IP" },
            ColumnWidths = new Dictionary<string, double> { ["Name"] = 200, ["IP"] = 120 },
            HiddenColumns = new List<string> { "InternalId" }
        };
        Assert.Equal(3, gs.ColumnOrder.Count);
        Assert.Equal(200, gs.ColumnWidths["Name"]);
        Assert.Equal(120, gs.ColumnWidths["IP"]);
        Assert.Single(gs.HiddenColumns);
    }

    [Fact]
    public void FormLayout_WithGroups()
    {
        var fl = new FormLayout
        {
            FieldOrder = new List<string> { "Name", "Status" },
            HiddenFields = new List<string> { "Id" },
            Groups = new List<FieldGroup>
            {
                new FieldGroup { Name = "Basic", Fields = new List<string> { "Name", "Status" } }
            }
        };
        Assert.NotNull(fl.Groups);
        Assert.Single(fl.Groups);
        Assert.Equal("Basic", fl.Groups[0].Name);
    }

    [Fact]
    public void FieldGroup_Collapsed()
    {
        var fg = new FieldGroup { Name = "Advanced", IsCollapsed = true };
        Assert.True(fg.IsCollapsed);
        Assert.Equal("Advanced", fg.Name);
    }

    [Fact]
    public void LinkRule_WithValues()
    {
        var lr = new LinkRule
        {
            SourcePanel = "IPAM", TargetPanel = "Links",
            SourceField = "SwitchName", TargetField = "DeviceA",
            FilterOnSelect = false
        };
        Assert.Equal("IPAM", lr.SourcePanel);
        Assert.Equal("Links", lr.TargetPanel);
        Assert.False(lr.FilterOnSelect);
    }

    [Fact]
    public void PanelCustomizationRecord_SetValues()
    {
        var pcr = new PanelCustomizationRecord
        {
            Id = 5, UserId = 10, PanelName = "IPAM",
            SettingType = "grid", SettingKey = "columns",
            SettingJson = "{\"order\":[\"Name\"]}"
        };
        Assert.Equal(5, pcr.Id);
        Assert.Equal("IPAM", pcr.PanelName);
        Assert.Contains("order", pcr.SettingJson);
    }
}
