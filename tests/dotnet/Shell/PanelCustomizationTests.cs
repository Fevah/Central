using Central.Engine.Models;

namespace Central.Tests.Shell;

public class PanelCustomizationTests
{
    [Fact]
    public void GridSettings_Defaults()
    {
        var gs = new GridSettings();
        Assert.Equal(25, gs.RowHeight);
        Assert.True(gs.UseAlternatingRows);
        Assert.True(gs.ShowSummaryFooter);
        Assert.False(gs.ShowGroupPanel);
        Assert.True(gs.ShowAutoFilterRow);
        Assert.Null(gs.ColumnOrder);
        Assert.Null(gs.HiddenColumns);
    }

    [Fact]
    public void FormLayout_Defaults()
    {
        var fl = new FormLayout();
        Assert.Null(fl.FieldOrder);
        Assert.Null(fl.HiddenFields);
        Assert.Null(fl.Groups);
    }

    [Fact]
    public void LinkRule_Defaults()
    {
        var lr = new LinkRule();
        Assert.Equal("", lr.SourcePanel);
        Assert.Equal("", lr.TargetPanel);
        Assert.Equal("", lr.SourceField);
        Assert.Equal("", lr.TargetField);
        Assert.True(lr.FilterOnSelect);
    }

    [Fact]
    public void FieldGroup_Defaults()
    {
        var fg = new FieldGroup();
        Assert.Equal("", fg.Name);
        Assert.Empty(fg.Fields);
        Assert.False(fg.IsCollapsed);
    }

    [Fact]
    public void GridSettings_Serialization_RoundTrip()
    {
        var original = new GridSettings
        {
            RowHeight = 30,
            UseAlternatingRows = false,
            ShowSummaryFooter = true,
            ShowGroupPanel = true,
            ShowAutoFilterRow = false,
            HiddenColumns = new List<string> { "Id", "CreatedAt" }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var restored = System.Text.Json.JsonSerializer.Deserialize<GridSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(30, restored!.RowHeight);
        Assert.False(restored.UseAlternatingRows);
        Assert.True(restored.ShowGroupPanel);
        Assert.Equal(2, restored.HiddenColumns!.Count);
        Assert.Contains("Id", restored.HiddenColumns);
    }

    [Fact]
    public void LinkRule_Serialization_RoundTrip()
    {
        var rules = new List<LinkRule>
        {
            new() { SourcePanel = "Devices", SourceField = "Building", TargetPanel = "Switches", TargetField = "Building", FilterOnSelect = true },
            new() { SourcePanel = "Users", SourceField = "Username", TargetPanel = "AuthEvents", TargetField = "Username", FilterOnSelect = false }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(rules);
        var restored = System.Text.Json.JsonSerializer.Deserialize<List<LinkRule>>(json);

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Count);
        Assert.Equal("Devices", restored[0].SourcePanel);
        Assert.False(restored[1].FilterOnSelect);
    }
}
