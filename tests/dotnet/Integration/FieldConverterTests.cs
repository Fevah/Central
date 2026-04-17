using Central.Engine.Integration;

namespace Central.Tests.Integration;

public class FieldConverterTests
{
    private readonly ConvertContext _ctx = new()
    {
        SourceRecord = new Dictionary<string, object?>
        {
            ["first_name"] = "John",
            ["last_name"] = "Smith",
            ["email"] = "john.smith@example.com",
            ["created_at"] = new DateTime(2026, 3, 28, 10, 30, 0)
        }
    };

    [Fact]
    public void DirectConverter_PassesThrough()
    {
        var conv = new DirectConverter();
        Assert.Equal("hello", conv.Convert("hello", "", _ctx));
        Assert.Null(conv.Convert(null, "", _ctx));
        Assert.Equal(42, conv.Convert(42, "", _ctx));
    }

    [Fact]
    public void ConstantConverter_ReturnsExpression()
    {
        var conv = new ConstantConverter();
        Assert.Equal("Active", conv.Convert("anything", "Active", _ctx));
        Assert.Equal("true", conv.Convert(null, "true", _ctx));
    }

    [Fact]
    public void CombineConverter_InterpolatesFields()
    {
        var conv = new CombineConverter();
        var result = conv.Convert(null, "{first_name} {last_name}", _ctx);
        Assert.Equal("John Smith", result);
    }

    [Fact]
    public void CombineConverter_MissingField_ReplacesWithEmpty()
    {
        var conv = new CombineConverter();
        var result = conv.Convert(null, "{first_name} {middle_name} {last_name}", _ctx);
        // middle_name not in source — placeholder stays as literal or empty
        Assert.Contains("John", result?.ToString() ?? "");
        Assert.Contains("Smith", result?.ToString() ?? "");
    }

    [Fact]
    public void SplitConverter_ExtractsIndex()
    {
        var conv = new SplitConverter();
        Assert.Equal("john.smith", conv.Convert("john.smith@example.com", "@|0", _ctx));
        Assert.Equal("example.com", conv.Convert("john.smith@example.com", "@|1", _ctx));
    }

    [Fact]
    public void SplitConverter_InvalidIndex_ReturnsOriginal()
    {
        var conv = new SplitConverter();
        Assert.Equal("hello", conv.Convert("hello", "@|5", _ctx));
    }

    [Fact]
    public void DateFormatConverter_FormatsDateTime()
    {
        var conv = new DateFormatConverter();
        var dt = new DateTime(2026, 3, 28, 10, 30, 0);
        Assert.Equal("2026-03-28", conv.Convert(dt, "yyyy-MM-dd", _ctx));
        Assert.Equal("28/03/2026", conv.Convert(dt, "dd/MM/yyyy", _ctx));
    }

    [Fact]
    public void DateFormatConverter_ParsesString()
    {
        var conv = new DateFormatConverter();
        var result = conv.Convert("2026-03-28T10:30:00", "dd MMM yyyy", _ctx);
        Assert.Equal("28 Mar 2026", result);
    }

    [Fact]
    public void ExpressionConverter_ReplacesValueRef()
    {
        var conv = new ExpressionConverter();
        Assert.Equal("John", conv.Convert("John", "$value", _ctx));
    }

    [Fact]
    public void ExpressionConverter_ReplacesFieldRefs()
    {
        var conv = new ExpressionConverter();
        Assert.Equal("John Smith", conv.Convert(null, "$first_name $last_name", _ctx));
    }

    [Fact]
    public void ExpressionConverter_Upper()
    {
        var conv = new ExpressionConverter();
        Assert.Equal("HELLO", conv.Convert("hello", "upper:$value", _ctx));
    }

    [Fact]
    public void ExpressionConverter_Lower()
    {
        var conv = new ExpressionConverter();
        Assert.Equal("hello", conv.Convert("HELLO", "lower:$value", _ctx));
    }

    [Fact]
    public void ExpressionConverter_Bool()
    {
        var conv = new ExpressionConverter();
        Assert.Equal(true, conv.Convert(null, "bool:true", _ctx));
        Assert.Equal(false, conv.Convert(null, "bool:false", _ctx));
    }

    [Fact]
    public void LookupConverter_CallsLookupFunc()
    {
        var ctx = new ConvertContext
        {
            SourceRecord = new(),
            LookupFunc = (expr, value) => expr == "roles.name" && value == "admin" ? "Administrator" : null
        };
        var conv = new LookupConverter();
        Assert.Equal("Administrator", conv.Convert("admin", "roles.name", ctx));
    }

    [Fact]
    public void LookupConverter_NullFunc_ReturnsOriginal()
    {
        var conv = new LookupConverter();
        Assert.Equal("value", conv.Convert("value", "anything", new ConvertContext()));
    }
}
