using Central.Core.Integration;

namespace Central.Tests.Integration;

public class ConverterEdgeCaseTests
{
    private readonly ConvertContext _emptyCtx = new() { SourceRecord = new() };

    [Fact]
    public void Direct_NullValue() => Assert.Null(new DirectConverter().Convert(null, "", _emptyCtx));

    [Fact]
    public void Direct_IntValue() => Assert.Equal(42, new DirectConverter().Convert(42, "", _emptyCtx));

    [Fact]
    public void Direct_BoolValue() => Assert.Equal(true, new DirectConverter().Convert(true, "", _emptyCtx));

    [Fact]
    public void Constant_AlwaysReturnsExpression()
    {
        Assert.Equal("fixed", new ConstantConverter().Convert(null, "fixed", _emptyCtx));
        Assert.Equal("fixed", new ConstantConverter().Convert("ignored", "fixed", _emptyCtx));
        Assert.Equal("fixed", new ConstantConverter().Convert(999, "fixed", _emptyCtx));
    }

    [Fact]
    public void Combine_EmptyExpression() => Assert.Equal("", new CombineConverter().Convert(null, "", _emptyCtx));

    [Fact]
    public void Split_NullValue() => Assert.Null(new SplitConverter().Convert(null, "|0", _emptyCtx));

    [Fact]
    public void Split_InvalidExpression() => Assert.Equal("hello", new SplitConverter().Convert("hello", "bad", _emptyCtx));

    [Fact]
    public void DateFormat_NullValue() => Assert.Null(new DateFormatConverter().Convert(null, "yyyy", _emptyCtx));

    [Fact]
    public void DateFormat_InvalidString() => Assert.Equal("not-a-date", new DateFormatConverter().Convert("not-a-date", "yyyy", _emptyCtx));

    [Fact]
    public void Expression_EmptyValue() => Assert.Equal("", new ExpressionConverter().Convert(null, "$value", _emptyCtx));

    [Fact]
    public void Lookup_NullValue() => Assert.Null(new LookupConverter().Convert(null, "table.col", _emptyCtx));
}
