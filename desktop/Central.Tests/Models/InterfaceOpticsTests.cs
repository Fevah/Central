using Central.Core.Models;

namespace Central.Tests.Models;

public class InterfaceOpticsTests
{
    // ── Display helpers ──

    [Fact]
    public void DisplayTx_WithValue()
    {
        var o = new InterfaceOptics { TxPowerDbm = -2.51m };
        Assert.Equal("-2.51 dBm", o.DisplayTx);
    }

    [Fact]
    public void DisplayTx_NoValue_Empty()
    {
        var o = new InterfaceOptics { TxPowerDbm = null };
        Assert.Equal("", o.DisplayTx);
    }

    [Fact]
    public void DisplayRx_WithValue()
    {
        var o = new InterfaceOptics { RxPowerDbm = -8.30m };
        Assert.Equal("-8.30 dBm", o.DisplayRx);
    }

    [Fact]
    public void DisplayTemp_WithValue()
    {
        var o = new InterfaceOptics { TempC = 35.5m };
        Assert.Contains("35.5", o.DisplayTemp);
        Assert.Contains("\u00B0C", o.DisplayTemp);
    }

    [Fact]
    public void DisplayTemp_NoValue_Empty()
    {
        var o = new InterfaceOptics { TempC = null };
        Assert.Equal("", o.DisplayTemp);
    }

    // ── RxColor ──

    [Theory]
    [InlineData(null, "#6B7280")]    // grey - no data
    [InlineData(-35, "#EF4444")]     // red - no light
    [InlineData(-30, "#EF4444")]     // red - exactly boundary
    [InlineData(-25, "#F59E0B")]     // yellow - marginal
    [InlineData(-20, "#F59E0B")]     // yellow - exactly boundary
    [InlineData(-10, "#22C55E")]     // green - ok
    [InlineData(0, "#22C55E")]       // green
    public void RxColor_ByPowerLevel(int? dbm, string expectedColor)
    {
        var o = new InterfaceOptics { RxPowerDbm = dbm.HasValue ? (decimal)dbm.Value : null };
        Assert.Equal(expectedColor, o.RxColor);
    }

    // ── Parse ──

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmpty()
    {
        var result = InterfaceOptics.Parse(Guid.NewGuid(), "");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NullOutput_ReturnsEmpty()
    {
        var result = InterfaceOptics.Parse(Guid.NewGuid(), null!);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceOutput_ReturnsEmpty()
    {
        var result = InterfaceOptics.Parse(Guid.NewGuid(), "   \n  \n  ");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_BasicSingleInterface()
    {
        var output = @"Interface          Temp(C/F)   Voltage  Bias(mA)        Tx Power(dBm)   Rx Power(dBm)   Module Type
---------          ---------   -------  --------        -------------   -------------   -----------
xe-1/1/1           26.00/78.80  3.42     5.78 [C1]       -0.21 [C1]      -8.50 [C1]      100G_BASE_AOC
";
        var switchId = Guid.NewGuid();
        var result = InterfaceOptics.Parse(switchId, output);

        Assert.True(result.Count >= 1);
        Assert.Equal("xe-1/1/1", result[0].InterfaceName);
        Assert.Equal(switchId, result[0].SwitchId);
        Assert.Equal(26.00m, result[0].TempC);
    }
}
