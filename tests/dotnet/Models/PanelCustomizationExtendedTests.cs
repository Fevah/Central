using Central.Engine.Models;

namespace Central.Tests.Models;

public class PanelCustomizationExtendedTests2
{
    // ── GridSettings extended ──

    [Fact]
    public void GridSettings_ColumnOrder_CanBeSet()
    {
        var gs = new GridSettings { ColumnOrder = new List<string> { "Name", "Status", "IP" } };
        Assert.Equal(3, gs.ColumnOrder!.Count);
        Assert.Equal("Name", gs.ColumnOrder[0]);
    }

    [Fact]
    public void GridSettings_ColumnWidths_CanBeSet()
    {
        var gs = new GridSettings
        {
            ColumnWidths = new Dictionary<string, double> { ["Name"] = 200.0, ["Status"] = 100.0 }
        };
        Assert.Equal(200.0, gs.ColumnWidths!["Name"]);
        Assert.Equal(100.0, gs.ColumnWidths!["Status"]);
    }

    [Fact]
    public void GridSettings_HiddenColumns_CanBeSet()
    {
        var gs = new GridSettings { HiddenColumns = new List<string> { "Id", "InternalRef" } };
        Assert.Equal(2, gs.HiddenColumns!.Count);
    }

    [Fact]
    public void GridSettings_NullCollections_AreDefault()
    {
        var gs = new GridSettings();
        Assert.Null(gs.ColumnOrder);
        Assert.Null(gs.ColumnWidths);
        Assert.Null(gs.HiddenColumns);
    }

    // ── FormLayout ──

    [Fact]
    public void FormLayout_Defaults()
    {
        var fl = new FormLayout();
        Assert.Null(fl.FieldOrder);
        Assert.Null(fl.HiddenFields);
        Assert.Null(fl.Groups);
    }

    [Fact]
    public void FormLayout_FieldOrder_CanBeSet()
    {
        var fl = new FormLayout { FieldOrder = new List<string> { "Name", "Status", "Description" } };
        Assert.Equal(3, fl.FieldOrder!.Count);
    }

    [Fact]
    public void FormLayout_HiddenFields_CanBeSet()
    {
        var fl = new FormLayout { HiddenFields = new List<string> { "InternalId" } };
        Assert.Single(fl.HiddenFields!);
    }

    [Fact]
    public void FormLayout_Groups_CanBeSet()
    {
        var fl = new FormLayout
        {
            Groups = new List<FieldGroup>
            {
                new() { Name = "General", Fields = new List<string> { "Name", "Status" } },
                new() { Name = "Details", Fields = new List<string> { "Description" }, IsCollapsed = true }
            }
        };
        Assert.Equal(2, fl.Groups!.Count);
        Assert.False(fl.Groups[0].IsCollapsed);
        Assert.True(fl.Groups[1].IsCollapsed);
    }

    // ── FieldGroup ──

    [Fact]
    public void FieldGroup_Defaults()
    {
        var fg = new FieldGroup();
        Assert.Equal("", fg.Name);
        Assert.NotNull(fg.Fields);
        Assert.Empty(fg.Fields);
        Assert.False(fg.IsCollapsed);
    }

    [Fact]
    public void FieldGroup_CanAddFields()
    {
        var fg = new FieldGroup { Name = "Network" };
        fg.Fields.Add("IP Address");
        fg.Fields.Add("Subnet");
        Assert.Equal(2, fg.Fields.Count);
    }

    // ── LinkRule extended ──

    [Fact]
    public void LinkRule_CanSetProperties()
    {
        var lr = new LinkRule
        {
            SourcePanel = "IPAM",
            TargetPanel = "Switches",
            SourceField = "Building",
            TargetField = "Site",
            FilterOnSelect = false
        };
        Assert.Equal("IPAM", lr.SourcePanel);
        Assert.Equal("Switches", lr.TargetPanel);
        Assert.Equal("Building", lr.SourceField);
        Assert.Equal("Site", lr.TargetField);
        Assert.False(lr.FilterOnSelect);
    }

    // ── PanelCustomizationRecord ──

    [Fact]
    public void PanelCustomizationRecord_Defaults()
    {
        var r = new PanelCustomizationRecord();
        Assert.Equal(0, r.Id);
        Assert.Equal(0, r.UserId);
        Assert.Equal("", r.PanelName);
        Assert.Equal("", r.SettingType);
        Assert.Equal("", r.SettingKey);
        Assert.Equal("{}", r.SettingJson);
    }

    [Fact]
    public void PanelCustomizationRecord_SetProperties()
    {
        var r = new PanelCustomizationRecord
        {
            Id = 1,
            UserId = 42,
            PanelName = "IPAM",
            SettingType = "grid",
            SettingKey = "columns",
            SettingJson = "{\"columns\": [\"Name\", \"IP\"]}"
        };
        Assert.Equal(1, r.Id);
        Assert.Equal(42, r.UserId);
        Assert.Equal("IPAM", r.PanelName);
        Assert.Equal("grid", r.SettingType);
        Assert.Equal("columns", r.SettingKey);
        Assert.Contains("Name", r.SettingJson);
    }
}
