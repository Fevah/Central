using Central.Engine.Models;

namespace Central.Tests.Models;

/// <summary>Tests for GridSettings, FormLayout, LinkRule, PanelCustomizationRecord models.</summary>
public class PanelCustomizationModelsTests
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
        Assert.Null(gs.ColumnWidths);
        Assert.Null(gs.HiddenColumns);
    }

    [Fact]
    public void GridSettings_CanSetProperties()
    {
        var gs = new GridSettings
        {
            RowHeight = 30,
            UseAlternatingRows = false,
            ShowGroupPanel = true,
            ColumnOrder = new List<string> { "Name", "Status" },
            ColumnWidths = new Dictionary<string, double> { ["Name"] = 200 },
            HiddenColumns = new List<string> { "Id" }
        };
        Assert.Equal(30, gs.RowHeight);
        Assert.False(gs.UseAlternatingRows);
        Assert.True(gs.ShowGroupPanel);
        Assert.Equal(2, gs.ColumnOrder!.Count);
        Assert.Equal(200, gs.ColumnWidths!["Name"]);
        Assert.Single(gs.HiddenColumns!);
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
    public void FieldGroup_Defaults()
    {
        var fg = new FieldGroup();
        Assert.Equal("", fg.Name);
        Assert.NotNull(fg.Fields);
        Assert.Empty(fg.Fields);
        Assert.False(fg.IsCollapsed);
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
    public void PanelCustomizationRecord_Defaults()
    {
        var pc = new PanelCustomizationRecord();
        Assert.Equal(0, pc.Id);
        Assert.Equal(0, pc.UserId);
        Assert.Equal("", pc.PanelName);
        Assert.Equal("", pc.SettingType);
        Assert.Equal("", pc.SettingKey);
        Assert.Equal("{}", pc.SettingJson);
    }
}
