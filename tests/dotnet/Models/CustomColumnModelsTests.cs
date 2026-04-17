using Central.Engine.Models;

namespace Central.Tests.Models;

public class CustomColumnModelsTests
{
    // ── CustomColumn ──

    [Fact]
    public void CustomColumn_Defaults()
    {
        var cc = new CustomColumn();
        Assert.Equal("Text", cc.ColumnType);
        Assert.Equal("", cc.Config);
        Assert.False(cc.IsRequired);
        Assert.Equal("", cc.DefaultValue);
    }

    [Fact]
    public void GetDropListOptions_EmptyConfig_ReturnsEmpty()
    {
        var cc = new CustomColumn { Config = "" };
        Assert.Empty(cc.GetDropListOptions());
    }

    [Fact]
    public void GetDropListOptions_ValidJson_ReturnsOptions()
    {
        var cc = new CustomColumn { Config = "{\"options\": [\"A\", \"B\", \"C\"]}" };
        var options = cc.GetDropListOptions();
        Assert.Equal(3, options.Length);
        Assert.Equal("A", options[0]);
        Assert.Equal("B", options[1]);
        Assert.Equal("C", options[2]);
    }

    [Fact]
    public void GetDropListOptions_NoOptionsKey_ReturnsEmpty()
    {
        var cc = new CustomColumn { Config = "{\"foo\": \"bar\"}" };
        Assert.Empty(cc.GetDropListOptions());
    }

    [Fact]
    public void GetDropListOptions_InvalidJson_ReturnsEmpty()
    {
        var cc = new CustomColumn { Config = "not json" };
        Assert.Empty(cc.GetDropListOptions());
    }

    [Fact]
    public void GetAggregationType_EmptyConfig_ReturnsNull()
    {
        var cc = new CustomColumn { Config = "" };
        Assert.Null(cc.GetAggregationType());
    }

    [Fact]
    public void GetAggregationType_ValidJson_ReturnsType()
    {
        var cc = new CustomColumn { Config = "{\"aggregation\": \"Sum\"}" };
        Assert.Equal("Sum", cc.GetAggregationType());
    }

    [Fact]
    public void GetAggregationType_NoKey_ReturnsNull()
    {
        var cc = new CustomColumn { Config = "{\"foo\": \"bar\"}" };
        Assert.Null(cc.GetAggregationType());
    }

    [Fact]
    public void GetAggregationType_InvalidJson_ReturnsNull()
    {
        var cc = new CustomColumn { Config = "invalid" };
        Assert.Null(cc.GetAggregationType());
    }

    [Fact]
    public void CustomColumn_PropertyChanged_Fires()
    {
        var cc = new CustomColumn();
        var changed = new List<string>();
        cc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        cc.Name = "Priority";
        Assert.Contains("Name", changed);
    }

    // ── TaskCustomValue ──

    [Theory]
    [InlineData("Text", "Hello", null, null, "Hello")]
    [InlineData("Number", null, 42.50, null, "42.50")]
    [InlineData("Hours", null, 8.00, null, "8.00")]
    [InlineData("Date", null, null, "2026-03-15", "2026-03-15")]
    [InlineData("DateTime", null, null, "2026-03-15 14:30", "2026-03-15 14:30")]
    public void TaskCustomValue_DisplayValue_ByType(string colType, string? text, double? number, string? dateStr, string expected)
    {
        var tcv = new TaskCustomValue { ColumnType = colType };
        if (text != null) tcv.ValueText = text;
        if (number.HasValue) tcv.ValueNumber = (decimal)number.Value;
        if (dateStr != null)
        {
            tcv.ValueDate = DateTime.Parse(dateStr);
        }
        Assert.Equal(expected, tcv.DisplayValue);
    }

    [Fact]
    public void TaskCustomValue_DisplayValue_NullValues_Empty()
    {
        var tcv = new TaskCustomValue { ColumnType = "Number" };
        Assert.Equal("", tcv.DisplayValue);
    }

    [Fact]
    public void TaskCustomValue_PropertyChanged_ValueText_NotifiesDisplayValue()
    {
        var tcv = new TaskCustomValue();
        var changed = new List<string>();
        tcv.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        tcv.ValueText = "new";
        Assert.Contains("ValueText", changed);
        Assert.Contains("DisplayValue", changed);
    }

    [Fact]
    public void TaskCustomValue_PropertyChanged_ValueNumber_NotifiesDisplayValue()
    {
        var tcv = new TaskCustomValue();
        var changed = new List<string>();
        tcv.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        tcv.ValueNumber = 5m;
        Assert.Contains("ValueNumber", changed);
        Assert.Contains("DisplayValue", changed);
    }

    [Fact]
    public void TaskCustomValue_PropertyChanged_ValueDate_NotifiesDisplayValue()
    {
        var tcv = new TaskCustomValue();
        var changed = new List<string>();
        tcv.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        tcv.ValueDate = DateTime.Today;
        Assert.Contains("ValueDate", changed);
        Assert.Contains("DisplayValue", changed);
    }

    // ── CustomColumnPermission ──

    [Fact]
    public void CustomColumnPermission_Defaults()
    {
        var perm = new CustomColumnPermission();
        Assert.True(perm.CanView);
        Assert.True(perm.CanEdit);
        Assert.Equal("", perm.GroupName);
    }
}
