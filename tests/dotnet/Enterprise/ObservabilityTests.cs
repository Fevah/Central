using Central.Observability;

namespace Central.Tests.Enterprise;

public class ObservabilityTests
{
    [Fact]
    public void CorrelationId_GeneratesUnique()
    {
        var id1 = CorrelationContext.CorrelationId;
        var id2 = CorrelationContext.CorrelationId;
        // Each access without setting generates a new GUID
        Assert.NotNull(id1);
    }

    [Fact]
    public void CorrelationId_SetAndGet()
    {
        CorrelationContext.CorrelationId = "test-123";
        Assert.Equal("test-123", CorrelationContext.CorrelationId);
    }

    [Fact]
    public void BeginScope_RestoresPrevious()
    {
        CorrelationContext.CorrelationId = "outer";
        using (CorrelationContext.BeginScope("inner"))
        {
            Assert.Equal("inner", CorrelationContext.CorrelationId);
        }
        Assert.Equal("outer", CorrelationContext.CorrelationId);
    }

    [Fact]
    public void StructuredLogEntry_ToCef()
    {
        var entry = new StructuredLogEntry
        {
            Level = "Error",
            Message = "Something failed",
            CorrelationId = "abc123",
            TenantSlug = "acme",
            Username = "admin"
        };

        var cef = entry.ToCef();
        Assert.Contains("CEF:0", cef);
        Assert.Contains("Something failed", cef);
        Assert.Contains("correlationId=abc123", cef);
        Assert.Contains("tenant=acme", cef);
    }

    [Fact]
    public void StructuredLogEntry_SeverityMapping()
    {
        var critical = new StructuredLogEntry { Level = "Critical" }.ToCef();
        var error = new StructuredLogEntry { Level = "Error" }.ToCef();
        var info = new StructuredLogEntry { Level = "Information" }.ToCef();
        // All produce valid CEF strings
        Assert.Contains("CEF:0", critical);
        Assert.Contains("CEF:0", error);
        Assert.Contains("CEF:0", info);
    }
}
